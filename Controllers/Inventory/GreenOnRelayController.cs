using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inventory
{
    /// <summary>
    /// Green On SSL termination + chunking relay for SAP.
    ///
    /// SAP STRUST does not trust GTS Root R4 (Google chain kartmax.in uses).
    /// SAP posts payload here over intranet HTTP; this endpoint forwards to
    /// Green On over HTTPS (Windows trust store handles Google chain natively)
    /// and slices large arrays into GREENON_CHUNK-sized POSTs — Green On
    /// returns 503 on payloads > ~5000 rows.
    ///
    /// POST /api/inv/greenon-relay
    ///   Body: JSON array of {sku, store_code, inventory}, or the wrapped
    ///         {action, inventoryData:[...]} envelope (pass-through).
    ///   Headers: X-RFC-Key (auth) — site_token + token + Origin injected here.
    ///
    /// Response:
    ///   { ok, http_code, body, latency_ms, batches, ok_batches, fail_batches }
    ///   ok=true iff every batch returned 2xx. http_code=worst status seen.
    ///
    /// When SAP STRUST cert is fixed, retire this endpoint — SAP FM can then
    /// call Green On directly (still needs client-side chunking).
    /// </summary>
    [RoutePrefix("api/inv")]
    public class GreenOnRelayController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";
        // URL / site_token / token / Origin are read from the HOST (secrets.config,
        // merged into appSettings by Web.config) so a tenant switch is a config edit
        // plus a pool recycle, not a rebuild. This repo is public: never put a real
        // production token in these literals. The compiled values below are the
        // pre-existing staging pair, kept as a fallback so a host with no override
        // behaves exactly as before.
        private static readonly string GREENON_URL = ReadSetting("GREENON_URL") ?? "https://engine.kartmax.in/api/import/catalogue-import-inventory";
        private static readonly string GREENON_SITE_TOKEN = ReadSetting("GREENON_SITE_TOKEN") ?? "UHwgPDz7YxPHimOYNEzg";
        // Rotated 2026-07-13: Green Honchos issued new token + Origin requirement.
        private static readonly string GREENON_API_TOKEN = ReadSetting("GREENON_API_TOKEN") ?? "e4f19c7a82b5d06ef93a1c74bd5802fa";
        private static readonly string GREENON_ORIGIN = ReadSetting("GREENON_ORIGIN") ?? "https://kxv2kart.kartmax.co";
        private const string GREENON_ACTION = "storeWiseInventory";
        // Green On 503s ~5K+ rows; 2K tested clean, 1K = safety margin.
        private const int GREENON_CHUNK = 1000;

        // appSettings (secrets.config) -> machine env var -> process env var.
        // Same three-tier lookup TownkartPushController uses for its tokens.
        private static string ReadSetting(string key)
        {
            string v = ConfigurationManager.AppSettings[key];
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            v = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        [HttpPost]
        [Route("greenon-relay")]
        public async System.Threading.Tasks.Task<IHttpActionResult> Relay()
        {
            IEnumerable<string> keyHeaders;
            bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
            if (!hasKey || keyHeaders.FirstOrDefault() != API_KEY)
            {
                return Json(new
                {
                    ok = false,
                    http_code = 401,
                    body = "Unauthorized — missing or invalid X-RFC-Key"
                });
            }

            string rawBody;
            try
            {
                rawBody = await Request.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    http_code = 400,
                    body = "Failed reading request body: " + ex.Message
                });
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Json(new { ok = false, http_code = 400, body = "Empty body" });
            }

            var started = DateTime.UtcNow;

            // Extract the inventoryData array whether caller sent raw array
            // or the wrapped envelope. Non-JSON / non-array bodies fall through
            // as single POST (unlikely, but preserves back-compat).
            JArray items = null;
            try
            {
                var trimmed = rawBody.TrimStart();
                if (trimmed.StartsWith("["))
                {
                    items = JArray.Parse(rawBody);
                }
                else if (trimmed.StartsWith("{"))
                {
                    var envelope = JObject.Parse(rawBody);
                    var invNode = envelope["inventoryData"];
                    if (invNode is JArray) items = (JArray)invNode;
                }
            }
            catch { /* leave items = null → single passthrough POST */ }

            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
                {
                    http.DefaultRequestHeaders.Add("site_token", GREENON_SITE_TOKEN);
                    http.DefaultRequestHeaders.Add("token", GREENON_API_TOKEN);
                    http.DefaultRequestHeaders.Add("Origin", GREENON_ORIGIN);

                    // Passthrough path: unparseable/no-array bodies go verbatim.
                    if (items == null)
                    {
                        var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
                        var resp = await http.PostAsync(GREENON_URL, content);
                        string respBody = await resp.Content.ReadAsStringAsync();
                        int code = (int)resp.StatusCode;
                        return ResponseMessage(new HttpResponseMessage((System.Net.HttpStatusCode)code)
                        {
                            Content = new StringContent(
                                JsonConvert.SerializeObject(new
                                {
                                    ok = resp.IsSuccessStatusCode,
                                    http_code = code,
                                    body = respBody,
                                    latency_ms = (int)(DateTime.UtcNow - started).TotalMilliseconds,
                                    batches = 1,
                                    ok_batches = resp.IsSuccessStatusCode ? 1 : 0,
                                    fail_batches = resp.IsSuccessStatusCode ? 0 : 1
                                }),
                                Encoding.UTF8, "application/json")
                        });
                    }

                    // Chunked path: slice inventoryData, POST sequentially.
                    int total = items.Count;
                    int batches = 0, ok_batches = 0, fail_batches = 0;
                    int worstCode = 200;
                    string firstError = null;

                    for (int off = 0; off < total; off += GREENON_CHUNK)
                    {
                        int take = Math.Min(GREENON_CHUNK, total - off);
                        var slice = new JArray();
                        for (int i = 0; i < take; i++) slice.Add(items[off + i]);

                        var envelope = new JObject
                        {
                            ["action"] = GREENON_ACTION,
                            ["inventoryData"] = slice
                        };
                        var body = envelope.ToString(Formatting.None);
                        var content = new StringContent(body, Encoding.UTF8, "application/json");

                        HttpResponseMessage resp;
                        int code;
                        string respBody;
                        try
                        {
                            resp = await http.PostAsync(GREENON_URL, content);
                            respBody = await resp.Content.ReadAsStringAsync();
                            code = (int)resp.StatusCode;
                        }
                        catch (Exception ex)
                        {
                            code = 599;
                            respBody = "Batch exception: " + ex.Message;
                        }

                        batches++;
                        if (code >= 200 && code < 300)
                        {
                            ok_batches++;
                        }
                        else
                        {
                            fail_batches++;
                            if (firstError == null)
                            {
                                firstError = "batch[" + off + "-" + (off + take - 1) + "] http=" + code + " body=" + respBody;
                            }
                            if (code > worstCode) worstCode = code;
                        }
                    }

                    bool allOk = fail_batches == 0;
                    int finalCode = allOk ? 200 : (worstCode >= 400 ? worstCode : 502);
                    return ResponseMessage(new HttpResponseMessage((System.Net.HttpStatusCode)finalCode)
                    {
                        Content = new StringContent(
                            JsonConvert.SerializeObject(new
                            {
                                ok = allOk,
                                http_code = finalCode,
                                body = allOk
                                    ? "{\"status\":true,\"message\":\"all " + total + " rows accepted across " + batches + " batches\"}"
                                    : firstError,
                                latency_ms = (int)(DateTime.UtcNow - started).TotalMilliseconds,
                                total_rows = total,
                                batches = batches,
                                ok_batches = ok_batches,
                                fail_batches = fail_batches
                            }),
                            Encoding.UTF8, "application/json")
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    http_code = 599,
                    body = "Relay exception: " + ex.Message,
                    latency_ms = (int)(DateTime.UtcNow - started).TotalMilliseconds
                });
            }
        }
    }
}
