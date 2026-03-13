using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vendor_SRM_Routing_Application.Controllers
{
    /// <summary>
    /// Data Lake Sync — runs SAP RFC calls (via local IIS) and upserts into Azure SQL via DAB.
    /// Triggered by Windows Task Scheduler at 02:00 IST daily.
    /// Job configs + results stored in Cloudflare KV so the CF Worker dashboard can read them.
    /// </summary>
    [RoutePrefix("api/Sync")]
    public class SyncController : ApiController
    {
        private const string CF_ACCOUNT  = "bab06c93e17ae71cae3c11b4cc40240b";
        private const string CF_KV_NS    = "f31b07a159dc4c3bbc2c06dc2c9fdafc";
        private const string CF_TOKEN    = "UiPONPWg2l0VbTVCitbkpZ-tu8gKvhgH42tCbsrZ";
        private const string DAB_BASE    = "https://my-dab-app.azurewebsites.net";
        private const string IIS_BASE    = "http://localhost";   // same IIS — call own RFC endpoints

        // ── GET /api/Sync/jobs ────────────────────────────────────────────────
        [HttpGet]
        [Route("jobs")]
        public async Task<IHttpActionResult> GetJobs()
        {
            try
            {
                var jobs = await GetAllJobsFromKV();
                return Ok(jobs);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ── POST /api/Sync/run-all ────────────────────────────────────────────
        [HttpPost]
        [Route("run-all")]
        public async Task<IHttpActionResult> RunAll()
        {
            var jobs = await GetAllJobsFromKV();
            var results = new JArray();
            foreach (var job in jobs)
            {
                if (job["enabled"]?.Value<bool>() == false) continue;
                var r = await SyncOne(job);
                results.Add(JObject.FromObject(r));
            }
            return Ok(results);
        }

        // ── POST /api/Sync/run/{rfcName} ──────────────────────────────────────
        [HttpPost]
        [Route("run/{rfcName}")]
        public async Task<IHttpActionResult> RunOne(string rfcName)
        {
            var raw = await KvGet("sync_job:" + rfcName);
            if (raw == null) return NotFound();
            var job = JObject.Parse(raw);
            var result = await SyncOne(job);
            return Ok(result);
        }

        // ── GET /api/Sync/poll — Windows Task polls every minute for queued triggers ──
        [HttpGet]
        [Route("poll")]
        public async Task<IHttpActionResult> Poll()
        {
            var ran = new JArray();
            using (var http = AuthedClient())
            {
                var listRes = await http.GetAsync(
                    $"https://api.cloudflare.com/client/v4/accounts/{CF_ACCOUNT}/storage/kv/namespaces/{CF_KV_NS}/keys?prefix=sync_trigger%3A");
                var listJson = JObject.Parse(await listRes.Content.ReadAsStringAsync());
                var keys = listJson["result"] as JArray ?? new JArray();

                foreach (var key in keys)
                {
                    var keyName = key["name"]?.ToString();                    // sync_trigger:{rfcName}
                    var rfcName = keyName?.Replace("sync_trigger:", "");
                    if (string.IsNullOrEmpty(rfcName)) continue;

                    // Delete the trigger first to avoid re-running on next poll
                    await http.DeleteAsync(
                        $"https://api.cloudflare.com/client/v4/accounts/{CF_ACCOUNT}/storage/kv/namespaces/{CF_KV_NS}/values/{Uri.EscapeDataString(keyName)}");

                    var raw = await KvGet("sync_job:" + rfcName);
                    if (raw == null) continue;
                    var job = JObject.Parse(raw);
                    var result = await SyncOne(job);
                    ran.Add(JObject.FromObject(result));
                }
            }
            return Ok(new { polled = ran.Count, results = ran });
        }

        // ════════════════════════════════════════════════════════════════════════
        // Core sync logic
        // ════════════════════════════════════════════════════════════════════════
        private async Task<object> SyncOne(JObject job)
        {
            var rfcName   = job["rfcName"]?.ToString();
            var tableName = job["tableName"]?.ToString();
            var prms      = (job["params"] as JObject) ?? new JObject();
            var ts        = DateTime.UtcNow.ToString("o");

            var result = new JObject
            {
                ["rfcName"]   = rfcName,
                ["tableName"] = tableName,
                ["startedAt"] = ts
            };

            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
                {
                    // ── Step 1: call own IIS RFC endpoint (SAP via VPN internally) ──
                    var formPairs = new List<KeyValuePair<string, string>>();
                    foreach (var p in prms.Properties())
                        formPairs.Add(new KeyValuePair<string, string>(p.Name, p.Value?.ToString() ?? ""));

                    var sapReq = new HttpRequestMessage(HttpMethod.Post, $"{IIS_BASE}/api/{rfcName}")
                    {
                        Content = new FormUrlEncodedContent(formPairs)
                    };
                    var sapRes = await http.SendAsync(sapReq);
                    if (!sapRes.IsSuccessStatusCode)
                        throw new Exception($"IIS {(int)sapRes.StatusCode}: {await sapRes.Content.ReadAsStringAsync()}");

                    var sapJson = JToken.Parse(await sapRes.Content.ReadAsStringAsync());
                    if (sapJson["Status"]?.ToString() == "E")
                        throw new Exception($"SAP error: {sapJson["Message"]}");

                    var rows = ExtractRows(sapJson);
                    result["rowsFetched"] = rows.Count;

                    // ── Step 2: upsert each row into Azure SQL via DAB REST API ──
                    int inserted = 0, failed = 0;
                    var failedSamples = new JArray();

                    foreach (var row in rows)
                    {
                        row["_SYNC_AT"] = ts;
                        row["_RFC"]     = rfcName;

                        var dabReq = new HttpRequestMessage(HttpMethod.Post, $"{DAB_BASE}/api/{tableName}")
                        {
                            Content = new StringContent(row.ToString(Formatting.None), Encoding.UTF8, "application/json")
                        };
                        var dabRes = await http.SendAsync(dabReq);
                        if (dabRes.IsSuccessStatusCode)
                        {
                            inserted++;
                        }
                        else
                        {
                            failed++;
                            if (failedSamples.Count < 3)
                            {
                                var errText = await dabRes.Content.ReadAsStringAsync();
                                failedSamples.Add(new JObject
                                {
                                    ["status"] = (int)dabRes.StatusCode,
                                    ["error"]  = errText.Length > 200 ? errText.Substring(0, 200) : errText
                                });
                            }
                        }
                    }

                    result["inserted"]     = inserted;
                    result["failed"]       = failed;
                    result["failedSamples"]= failedSamples;
                    result["status"]       = failed == 0 ? (rows.Count == 0 ? "empty" : "ok") : "partial";
                    result["finishedAt"]   = DateTime.UtcNow.ToString("o");
                }
            }
            catch (Exception ex)
            {
                result["status"]     = "error";
                result["error"]      = ex.Message;
                result["finishedAt"] = DateTime.UtcNow.ToString("o");
            }

            // Write result back to CF KV — 7 day TTL
            await KvPut("sync_result:" + rfcName, result.ToString(Formatting.None), 86400 * 7);
            return result;
        }

        private List<JObject> ExtractRows(JToken data)
        {
            var list = new List<JObject>();
            if (data is JArray rootArr)
            {
                foreach (var item in rootArr) if (item is JObject o) list.Add(o);
                return list;
            }
            var d = data["Data"] ?? data["data"];
            if (d == null) return list;
            if (d is JArray arr2) { foreach (var item in arr2) if (item is JObject o) list.Add(o); return list; }
            if (d is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (prop.Name == "EX_RETURN" || prop.Name == "ES_RETURN") continue;
                    if (prop.Value is JArray tableArr)
                    {
                        foreach (var item in tableArr) if (item is JObject o) list.Add(o);
                        return list;
                    }
                }
            }
            return list;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Cloudflare KV helpers
        // ════════════════════════════════════════════════════════════════════════
        private HttpClient AuthedClient()
        {
            var c = new HttpClient();
            c.DefaultRequestHeaders.Add("Authorization", "Bearer " + CF_TOKEN);
            return c;
        }

        private async Task<List<JObject>> GetAllJobsFromKV()
        {
            var jobs = new List<JObject>();
            using (var http = AuthedClient())
            {
                var listRes = await http.GetAsync(
                    $"https://api.cloudflare.com/client/v4/accounts/{CF_ACCOUNT}/storage/kv/namespaces/{CF_KV_NS}/keys?prefix=sync_job%3A");
                var listJson = JObject.Parse(await listRes.Content.ReadAsStringAsync());
                var keys = listJson["result"] as JArray ?? new JArray();
                foreach (var key in keys)
                {
                    var raw = await KvGet(key["name"]?.ToString());
                    if (raw != null) jobs.Add(JObject.Parse(raw));
                }
            }
            return jobs;
        }

        private async Task<string> KvGet(string key)
        {
            using (var http = AuthedClient())
            {
                var res = await http.GetAsync(
                    $"https://api.cloudflare.com/client/v4/accounts/{CF_ACCOUNT}/storage/kv/namespaces/{CF_KV_NS}/values/{Uri.EscapeDataString(key)}");
                return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : null;
            }
        }

        private async Task KvPut(string key, string value, int ttlSeconds)
        {
            using (var http = AuthedClient())
            {
                var content = new MultipartFormDataContent
                {
                    { new StringContent(value),                  "value" },
                    { new StringContent(ttlSeconds.ToString()),  "expiration_ttl" }
                };
                await http.PutAsync(
                    $"https://api.cloudflare.com/client/v4/accounts/{CF_ACCOUNT}/storage/kv/namespaces/{CF_KV_NS}/values/{Uri.EscapeDataString(key)}",
                    content);
            }
        }
    }
}
