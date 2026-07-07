using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inventory
{
    /// <summary>
    /// Green On e-commerce store-wise inventory feed.
    ///
    /// FM: Z_INV_STOREWISE_V1 (FG ZINV_RFC11, TR S4DK926712 LIVE DEV+QA 2026-07-07).
    /// Source: SUM(MARD.LABST) grouped by MATNR+WERKS, LGORT IN ('0001','0002').
    /// Filter: T001W.VLFKZ='A' (retail stores only, excludes DC/HUB where VLFKZ='B').
    /// Payload shape matches Green On: [{"sku":"1110002515001","store_code":"HB07","inventory":93}].
    /// MATNR sent with leading zeros stripped. WERKS raw. Inventory truncated to integer.
    ///
    /// Endpoint:
    ///   POST /api/inv/greenon-push
    ///     ?env=dev|qa|prod         (default dev)
    ///     &werks=CSV|all           (default all — auto-fetch T001W VLFKZ='A')
    ///     &dryRun=true|false       (default true — build payload, DO NOT POST to Green On)
    ///     &chunkSize=500           (Green On batch size)
    ///     &werksBatch=25           (WERKS per FM call — MARD read chunking)
    ///     &minQty=1                (skip SKUs below this qty)
    ///
    /// Auth: X-RFC-Key: v2-rfc-proxy-2026
    /// </summary>
    [RoutePrefix("api/inv")]
    public class GreenOnPushController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";

        // Green On endpoint + auth (provided by Green On 2026-07-07)
        private const string GREENON_URL = "https://engine.kartmax.in/api/import/catalogue-import-inventory";
        private const string GREENON_SITE_TOKEN = "UHwgPDz7YxPHimOYNEzg";
        private const string GREENON_API_TOKEN = "UshlJr1FhG3tuXNN4ijf5az2adf7453dfsps";

        [HttpPost]
        [Route("greenon-push")]
        public IHttpActionResult Push(
            string env = "dev",
            string werks = "all",
            bool dryRun = true,
            int chunkSize = 500,
            int werksBatch = 25,
            int minQty = 1)
        {
            // ── Auth ──────────────────────────────────────────────────────
            IEnumerable<string> keyHeaders;
            bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
            if (!hasKey || keyHeaders.FirstOrDefault() != API_KEY)
            {
                return Json(new
                {
                    Status = false,
                    Message = "Unauthorized — missing or invalid X-RFC-Key"
                });
            }

            RfcConfigParameters rfcPar;
            HttpResponseMessage envCheck = ResolveEnv(env, out rfcPar);
            if (envCheck != null)
            {
                return Json(new { Status = false, Message = "Invalid env '" + env + "'" });
            }

            RfcDestination dest;
            try
            {
                dest = RfcDestinationManager.GetDestination(rfcPar);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = "RFC connect failed: " + ex.Message });
            }

            // ── Resolve WERKS list ────────────────────────────────────────
            List<string> werksList;
            try
            {
                werksList = ResolveWerks(dest, werks);
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = "WERKS resolve failed: " + ex.Message });
            }

            if (werksList.Count == 0)
            {
                return Json(new { Status = false, Message = "No WERKS in scope" });
            }

            // ── Call FM per WERKS chunk, merge JSON items ─────────────────
            List<JObject> allItems = new List<JObject>();
            List<JObject> fmErrors = new List<JObject>();
            int fmCallCount = 0;

            for (int i = 0; i < werksList.Count; i += werksBatch)
            {
                var chunk = werksList.Skip(i).Take(werksBatch).ToList();
                string csv = string.Join(",", chunk);
                fmCallCount++;

                try
                {
                    IRfcFunction fn = dest.Repository.CreateFunction("Z_INV_STOREWISE_V1");
                    fn.SetValue("IV_WERKS_CSV", csv);
                    fn.SetValue("IV_MIN_QTY", minQty);
                    fn.Invoke(dest);

                    string jsonStr = fn.GetString("EV_JSON") ?? "[]";
                    if (jsonStr.Length > 2)
                    {
                        JArray arr = JArray.Parse(jsonStr);
                        foreach (JObject item in arr) allItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    fmErrors.Add(new JObject
                    {
                        ["werks_chunk"] = csv,
                        ["error"] = ex.Message
                    });
                }
            }

            int totalSkus = allItems.Count;

            // ── Dry run: return preview only ──────────────────────────────
            if (dryRun)
            {
                return Json(new
                {
                    Status = true,
                    Env = env,
                    DryRun = true,
                    WerksInScope = werksList.Count,
                    FmCalls = fmCallCount,
                    FmErrors = fmErrors,
                    TotalSkus = totalSkus,
                    WouldBeBatches = (totalSkus + chunkSize - 1) / chunkSize,
                    ChunkSize = chunkSize,
                    SamplePayload = new
                    {
                        action = "storeWiseInventory",
                        inventoryData = allItems.Take(5).ToList()
                    },
                    GreenOnUrl = GREENON_URL
                });
            }

            // ── Live push: chunk + POST to Green On ───────────────────────
            List<JObject> batchResults = new List<JObject>();
            int pushed = 0, failed = 0;

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
            {
                http.DefaultRequestHeaders.Add("site_token", GREENON_SITE_TOKEN);
                http.DefaultRequestHeaders.Add("token", GREENON_API_TOKEN);

                for (int i = 0; i < allItems.Count; i += chunkSize)
                {
                    var batch = allItems.Skip(i).Take(chunkSize).ToList();
                    int batchNo = (i / chunkSize) + 1;

                    var payload = new
                    {
                        action = "storeWiseInventory",
                        inventoryData = batch
                    };
                    string bodyJson = JsonConvert.SerializeObject(payload);

                    try
                    {
                        var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                        var resp = http.PostAsync(GREENON_URL, content).Result;
                        string respBody = resp.Content.ReadAsStringAsync().Result;
                        bool ok = resp.IsSuccessStatusCode;
                        if (ok) pushed += batch.Count; else failed += batch.Count;

                        batchResults.Add(new JObject
                        {
                            ["batch"] = batchNo,
                            ["skus"] = batch.Count,
                            ["http_status"] = (int)resp.StatusCode,
                            ["ok"] = ok,
                            ["response_snippet"] = respBody.Length > 500 ? respBody.Substring(0, 500) : respBody
                        });
                    }
                    catch (Exception ex)
                    {
                        failed += batch.Count;
                        batchResults.Add(new JObject
                        {
                            ["batch"] = batchNo,
                            ["skus"] = batch.Count,
                            ["ok"] = false,
                            ["error"] = ex.Message
                        });
                    }
                }
            }

            return Json(new
            {
                Status = failed == 0,
                Env = env,
                DryRun = false,
                WerksInScope = werksList.Count,
                FmCalls = fmCallCount,
                FmErrors = fmErrors,
                TotalSkus = totalSkus,
                Pushed = pushed,
                Failed = failed,
                Batches = batchResults
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private List<string> ResolveWerks(RfcDestination dest, string werksParam)
        {
            // Explicit CSV — trust caller
            if (!string.IsNullOrWhiteSpace(werksParam) &&
                !werksParam.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return werksParam.Split(',')
                    .Select(w => w.Trim().ToUpperInvariant())
                    .Where(w => w.Length > 0 && w.Length <= 4)
                    .Distinct()
                    .ToList();
            }

            // Auto-fetch retail stores (VLFKZ='A' — excludes DC/HUB where VLFKZ='B')
            IRfcFunction fn = dest.Repository.CreateFunction("RFC_READ_TABLE");
            fn.SetValue("QUERY_TABLE", "T001W");

            IRfcTable opt = fn.GetTable("OPTIONS");
            IRfcStructure optRow = opt.Metadata.LineType.CreateStructure();
            optRow.SetValue("TEXT", "VLFKZ = 'A'");
            opt.Append(optRow);

            IRfcTable fld = fn.GetTable("FIELDS");
            IRfcStructure fldRow = fld.Metadata.LineType.CreateStructure();
            fldRow.SetValue("FIELDNAME", "WERKS");
            fld.Append(fldRow);

            fn.Invoke(dest);

            IRfcTable data = fn.GetTable("DATA");
            List<string> list = new List<string>();
            foreach (IRfcStructure row in data)
            {
                string wa = row.GetString("WA") ?? "";
                string w = wa.Length >= 4 ? wa.Substring(0, 4).Trim() : wa.Trim();
                if (w.Length > 0) list.Add(w);
            }
            return list.Distinct().OrderBy(w => w).ToList();
        }

        private HttpResponseMessage ResolveEnv(string env, out RfcConfigParameters rfcPar)
        {
            rfcPar = null;
            string envNorm = (env ?? "dev").Trim().ToLowerInvariant();
            switch (envNorm)
            {
                case "qa":
                case "quality":
                    rfcPar = BaseController.rfcConfigparametersquality();
                    return null;
                case "prod":
                case "production":
                    rfcPar = BaseController.rfcConfigparametersproduction();
                    return null;
                case "dev":
                case "development":
                case "":
                    rfcPar = BaseController.rfcConfigparameters();
                    return null;
                default:
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Status = false, Message = "Invalid env '" + env + "'" });
            }
        }
    }
}
