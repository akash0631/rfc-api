using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inventory
{
    /// <summary>
    /// Townkart V2 (Unthinkable) store-wise inventory push.
    ///
    /// Spec: "Store App Inventory Push to Townkart V2 API" v1.0, 20-Aug-2026
    ///       (Nikhil Chhokra). Sibling of GreenOnPushController — same SAP
    ///       reader, different envelope and different vendor.
    ///
    /// FM: Z_INV_STOREWISE_V1 (FG ZINV_RFC11) — SUM(MARD.LABST) by MATNR+WERKS
    ///     over LGORT 0001+0002, MTART restricted to APP/GM/FBG. That is
    ///     exactly the selection in spec section 6.
    ///     V1 hard-floors IV_MIN_QTY to 1, so it cannot emit qty 0. Flip ?fm=
    ///     to Z_INV_STOREWISE_V2 with minQty=-1 once V2 is live — see the
    ///     zero-quantity note below.
    ///
    /// Envelope difference vs Green On: Townkart carries storeCode and
    /// businessDate at HEADER level, so one POST covers exactly one site.
    /// Payloads are therefore grouped per site, then batched.
    ///
    /// itemSKU is the 13-digit variant material number, NOT the EAN. Spec
    /// assumption A3 says EAN; MARA disagrees — variant MATNR is 13-digit
    /// (1110002049002), the generic SATNR is 10-digit (1110002049) and EAN11
    /// is a 7-digit code that is blank on much of the range. The vendor's own
    /// sample itemSKU 1114056195031 is 13-digit, i.e. a variant MATNR.
    /// remarks therefore carries SATNR + "-" + site, per spec section 8.2.
    ///
    /// ZERO QUANTITY (spec 8.3): adjustmentType REPLACE means Townkart only
    /// touches SKUs present in the payload. A sold-out SKU that drops out of
    /// the extract keeps its previous quantity and stays orderable. Sending
    /// qty 0 requires Z_INV_STOREWISE_V2 (no min-qty floor). Until then this
    /// controller runs spec Option 2 and the overselling window is open.
    ///
    /// Endpoint:
    ///   POST /api/inv/townkart-push
    ///     ?env=dev|qa|prod       (default dev — FM currently exists on DEV only)
    ///     &amp;werks=HA10,HA11|all    (default HA10 — "all" needs confirm, see below)
    ///     &amp;dryRun=true|false     (default TRUE — builds payload, no vendor call)
    ///     &amp;businessDate=2026-08-20 (default: server date, IST)
    ///     &amp;chunkSize=500          (records per POST, spec section 5)
    ///     &amp;minQty=1               (-1 = no floor, needs fm=Z_INV_STOREWISE_V2)
    ///     &amp;seg=APP,GM,FBG         (MTART segments)
    ///     &amp;fm=Z_INV_STOREWISE_V1  (whitelisted reader FM)
    ///     &amp;confirm=ALL_STORES     (required to live-push werks=all)
    ///
    /// Auth (ours): X-RFC-Key: v2-rfc-proxy-2026
    /// Auth (theirs): Authorization Bearer + X-Api-Key + X-Api-Token, read from
    ///   config keys TOWNKART_TOKEN / TOWNKART_API_KEY. Never hardcode them —
    ///   this repo is public. See Web.config appSettings file="secrets.config".
    /// </summary>
    [RoutePrefix("api/inv")]
    public class TownkartPushController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";

        private const string TOWNKART_URL =
            "https://backend-v2.townkart.ai/api/v2/store/sku-inventory";
        private const string TOWNKART_SYNC_URL =
            "https://backend-v2.townkart.ai/api/custom/inventory/Inventory/sync-inventory-from-sap";

        // Fixed per spec section 8.2. Constant for every record.
        private const string SHELF_CODE = "Default";
        private const string INVENTORY_TYPE = "GOOD_INVENTORY";
        private const string SLA = "000";
        private const string ADJUSTMENT_TYPE = "REPLACE";
        private const string FACILITY_CODE = "V2KART";

        // Spec section 11: 5xx and timeout retry 3 times, 5s / 15s / 45s.
        private static readonly int[] RETRY_WAITS_MS = { 5000, 15000, 45000 };
        private const int HTTP_TIMEOUT_SEC = 60;

        private static readonly string[] ALLOWED_FMS =
        {
            "Z_INV_STOREWISE_V1",
            "Z_INV_STOREWISE_V2"
        };

        [HttpPost]
        [Route("townkart-push")]
        public async Task<IHttpActionResult> Push(
            string env = "dev",
            string werks = "HA10",
            bool dryRun = true,
            string businessDate = null,
            int chunkSize = 500,
            int minQty = 1,
            string seg = "APP,GM,FBG",
            string fm = "Z_INV_STOREWISE_V1",
            string confirm = null,
            bool sync = false)
        {
            var runStart = DateTime.Now;

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

            string fmName = (fm ?? "").Trim().ToUpperInvariant();
            if (!ALLOWED_FMS.Contains(fmName))
            {
                return Json(new
                {
                    Status = false,
                    Message = "fm must be one of: " + string.Join(", ", ALLOWED_FMS)
                });
            }

            if (chunkSize < 1 || chunkSize > 5000)
            {
                return Json(new { Status = false, Message = "chunkSize must be 1..5000" });
            }

            // Spec section 5: businessDate is the snapshot date in IST.
            // Server local time is IST; A8 (IST vs UTC) is still unconfirmed.
            string bizDate;
            if (string.IsNullOrWhiteSpace(businessDate))
            {
                bizDate = runStart.ToString("yyyy-MM-dd");
            }
            else
            {
                DateTime parsed;
                if (!DateTime.TryParseExact(businessDate.Trim(), "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out parsed))
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "businessDate must be yyyy-MM-dd"
                    });
                }
                bizDate = parsed.ToString("yyyy-MM-dd");
            }

            RfcConfigParameters rfcPar;
            if (!TryResolveEnv(env, out rfcPar))
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

            bool wantsAll = !string.IsNullOrWhiteSpace(werks) &&
                            werks.Trim().Equals("all", StringComparison.OrdinalIgnoreCase);

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

            // A live all-stores push sent 6.9M unrequested rows to the other
            // vendor on 2026-07-13. Fan-out that wide has to be typed out.
            if (wantsAll && !dryRun && confirm != "ALL_STORES")
            {
                return Json(new
                {
                    Status = false,
                    Message = "Live push to all " + werksList.Count + " stores needs " +
                              "&confirm=ALL_STORES. Run with dryRun=true first."
                });
            }

            string bearer = ReadSetting("TOWNKART_TOKEN");
            string apiKey = ReadSetting("TOWNKART_API_KEY");
            if (!dryRun && (string.IsNullOrWhiteSpace(bearer) || string.IsNullOrWhiteSpace(apiKey)))
            {
                return Json(new
                {
                    Status = false,
                    Message = "TOWNKART_TOKEN / TOWNKART_API_KEY not configured on this host. " +
                              "Set them in secrets.config (or as machine environment variables). " +
                              "Never commit them — this repo is public."
                });
            }

            var siteResults = new List<JObject>();
            int grandSkus = 0, grandPushed = 0, grandFailed = 0;
            int grandClamped = 0, grandBatches = 0;
            bool abortRun = false;
            string abortReason = null;
            object samplePayload = null;

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SEC) })
            {
                if (!dryRun)
                {
                    http.DefaultRequestHeaders.TryAddWithoutValidation(
                        "Authorization", "Bearer " + bearer);
                    http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
                    http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Token", bearer);
                }

                // Spec section 13: sites run one after another, and a failure on
                // one site must not stop the rest.
                foreach (string site in werksList)
                {
                    if (abortRun) break;

                    var siteStart = DateTime.Now;
                    JArray rows;
                    int fmCount;
                    try
                    {
                        rows = ReadStock(dest, fmName, site, minQty, seg, out fmCount);
                    }
                    catch (Exception ex)
                    {
                        siteResults.Add(new JObject
                        {
                            ["store_code"] = site,
                            ["status"] = "Failed",
                            ["error"] = "FM " + fmName + " failed: " + ex.Message
                        });
                        continue;
                    }

                    int clamped;
                    JArray items = MapItems(rows, site, out clamped);
                    grandSkus += items.Count;
                    grandClamped += clamped;

                    if (samplePayload == null && items.Count > 0)
                    {
                        samplePayload = new
                        {
                            storeCode = site,
                            businessDate = bizDate,
                            inventoryData = items.Take(2).ToList()
                        };
                    }

                    int siteBatches = (items.Count + chunkSize - 1) / chunkSize;
                    grandBatches += siteBatches;

                    if (dryRun || items.Count == 0)
                    {
                        siteResults.Add(new JObject
                        {
                            ["store_code"] = site,
                            ["status"] = items.Count == 0 ? "No stock" : "Dry run",
                            ["fm_count"] = fmCount,
                            ["skus"] = items.Count,
                            ["negatives_clamped"] = clamped,
                            ["would_be_batches"] = siteBatches,
                            ["duration_ms"] = (int)(DateTime.Now - siteStart).TotalMilliseconds
                        });
                        continue;
                    }

                    var batchLog = new JArray();
                    int okBatches = 0, failBatches = 0;

                    for (int off = 0; off < items.Count; off += chunkSize)
                    {
                        int take = Math.Min(chunkSize, items.Count - off);
                        var slice = new JArray();
                        for (int i = 0; i < take; i++) slice.Add(items[off + i]);

                        var payload = new JObject
                        {
                            ["storeCode"] = site,
                            ["businessDate"] = bizDate,
                            ["inventoryData"] = slice
                        };

                        BatchOutcome outcome = await PostWithRetry(
                            http, TOWNKART_URL, payload.ToString(Formatting.None));

                        batchLog.Add(new JObject
                        {
                            ["batch"] = (off / chunkSize) + 1,
                            ["skus"] = take,
                            ["http_code"] = outcome.Code,
                            ["ok"] = outcome.Ok,
                            ["attempts"] = outcome.Attempts,
                            ["latency_ms"] = outcome.LatencyMs,
                            ["response"] = Snip(outcome.Body)
                        });

                        if (outcome.Ok)
                        {
                            okBatches++;
                            grandPushed += take;
                        }
                        else
                        {
                            failBatches++;
                            grandFailed += take;

                            // Spec section 11: a bad credential silently stops
                            // every site. Stop the run, do not grind through 550.
                            if (outcome.Code == 401 || outcome.Code == 403)
                            {
                                abortRun = true;
                                abortReason = "HTTP " + outcome.Code +
                                              " from Townkart — token expired or invalid. " +
                                              "Run aborted at site " + site + ".";
                                break;
                            }
                        }
                    }

                    siteResults.Add(new JObject
                    {
                        ["store_code"] = site,
                        ["status"] = failBatches == 0 ? "Success"
                                   : (okBatches > 0 ? "Partially Successful" : "Failed"),
                        ["fm_count"] = fmCount,
                        ["skus"] = items.Count,
                        ["negatives_clamped"] = clamped,
                        ["batches_sent"] = okBatches + failBatches,
                        ["batches_ok"] = okBatches,
                        ["batches_failed"] = failBatches,
                        ["duration_ms"] = (int)(DateTime.Now - siteStart).TotalMilliseconds,
                        ["batch_log"] = batchLog
                    });
                }

                // Spec O7: unconfirmed whether this must follow the push.
                // Opt-in only until Townkart answers.
                JObject syncResult = null;
                if (sync && !dryRun && !abortRun)
                {
                    BatchOutcome s = await PostWithRetry(http, TOWNKART_SYNC_URL, "{}");
                    syncResult = new JObject
                    {
                        ["http_code"] = s.Code,
                        ["ok"] = s.Ok,
                        ["latency_ms"] = s.LatencyMs,
                        ["response"] = Snip(s.Body)
                    };
                }

                string runStatus =
                    abortRun ? "Failed"
                    : dryRun ? "Dry run"
                    : grandFailed == 0 ? "Success"
                    : grandPushed > 0 ? "Partially Successful" : "Failed";

                return Json(new
                {
                    Status = !abortRun && (dryRun || grandFailed == 0),
                    RunStatus = runStatus,
                    AbortReason = abortReason,
                    Env = env,
                    Fm = fmName,
                    DryRun = dryRun,
                    BusinessDate = bizDate,
                    MinQty = minQty,
                    Seg = seg,
                    SitesInScope = werksList.Count,
                    SitesProcessed = siteResults.Count,
                    TotalSkus = grandSkus,
                    NegativesClamped = grandClamped,
                    Batches = grandBatches,
                    Pushed = grandPushed,
                    Failed = grandFailed,
                    ZeroQtyCaveat = minQty > 0
                        ? "minQty=" + minQty + " — sold-out SKUs are omitted, so Townkart " +
                          "keeps their previous quantity (spec 8.3 Option 2). Needs " +
                          "Z_INV_STOREWISE_V2 + minQty=-1 to send zeros."
                        : null,
                    TownkartUrl = TOWNKART_URL,
                    SamplePayload = samplePayload,
                    SyncCall = syncResult,
                    StartedAt = runStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DurationMs = (int)(DateTime.Now - runStart).TotalMilliseconds,
                    Sites = siteResults
                });
            }
        }

        // ── SAP read ──────────────────────────────────────────────────────

        private JArray ReadStock(RfcDestination dest, string fmName, string site,
                                 int minQty, string seg, out int fmCount)
        {
            IRfcFunction fn = dest.Repository.CreateFunction(fmName);
            fn.SetValue("IV_WERKS_CSV", site);
            fn.SetValue("IV_MIN_QTY", minQty);
            if (!string.IsNullOrWhiteSpace(seg)) fn.SetValue("IV_SEG_CSV", seg);
            fn.Invoke(dest);

            fmCount = fn.GetInt("EV_COUNT");
            string json = fn.GetString("EV_JSON");
            if (string.IsNullOrWhiteSpace(json) || json.Length <= 2) return new JArray();
            return JArray.Parse(json);
        }

        // ── Mapping (spec section 8.2) ────────────────────────────────────

        private JArray MapItems(JArray rows, string site, out int clamped)
        {
            clamped = 0;
            var items = new JArray();

            foreach (JObject row in rows.OfType<JObject>())
            {
                string sku = (string)row["sku"];
                if (string.IsNullOrWhiteSpace(sku)) continue;

                int qty = row["inventory"] != null ? (int)row["inventory"] : 0;
                if (qty < 0)
                {
                    // Spec 6.1: negative aggregate is clamped to 0 and flagged.
                    qty = 0;
                    clamped++;
                }

                items.Add(new JObject
                {
                    ["itemSKU"] = sku,
                    ["quantity"] = qty,
                    ["shelfCode"] = SHELF_CODE,
                    ["inventoryType"] = INVENTORY_TYPE,
                    ["sla"] = SLA,
                    ["adjustmentType"] = ADJUSTMENT_TYPE,
                    ["remarks"] = GenericArticle(sku) + "-" + site,
                    ["facilityCode"] = FACILITY_CODE
                });
            }

            return items;
        }

        /// <summary>
        /// Generic article (MARA.SATNR) for a variant material. V2 variants are
        /// 13-digit and their SATNR is the leading 10 — 1110002049002 rolls up
        /// to 1110002049. Materials that are already 10-digit are their own
        /// generic and pass through unchanged.
        /// </summary>
        private static string GenericArticle(string sku)
        {
            return sku.Length > 10 ? sku.Substring(0, 10) : sku;
        }

        // ── HTTP with retry (spec section 11) ─────────────────────────────

        private class BatchOutcome
        {
            public bool Ok;
            public int Code;
            public string Body;
            public int Attempts;
            public int LatencyMs;
        }

        private async Task<BatchOutcome> PostWithRetry(HttpClient http, string url, string body)
        {
            var started = DateTime.UtcNow;
            var result = new BatchOutcome { Code = 0, Body = "", Attempts = 0 };

            for (int attempt = 0; attempt <= RETRY_WAITS_MS.Length; attempt++)
            {
                result.Attempts = attempt + 1;
                int waitMs = -1;

                try
                {
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    HttpResponseMessage resp = await http.PostAsync(url, content);
                    result.Code = (int)resp.StatusCode;
                    result.Body = await resp.Content.ReadAsStringAsync();

                    if (resp.IsSuccessStatusCode)
                    {
                        result.Ok = true;
                        break;
                    }

                    if (result.Code == 429)
                    {
                        // Honour Retry-After when the vendor sends one.
                        int retryAfter = 0;
                        if (resp.Headers.RetryAfter != null &&
                            resp.Headers.RetryAfter.Delta.HasValue)
                        {
                            retryAfter = (int)resp.Headers.RetryAfter.Delta.Value.TotalMilliseconds;
                        }
                        waitMs = retryAfter > 0
                            ? retryAfter
                            : (attempt < RETRY_WAITS_MS.Length ? RETRY_WAITS_MS[attempt] : -1);
                    }
                    else if (result.Code >= 500)
                    {
                        waitMs = attempt < RETRY_WAITS_MS.Length ? RETRY_WAITS_MS[attempt] : -1;
                    }
                    else
                    {
                        // 400 / 401 / 403 / 404 — never retry (spec section 11).
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Timeout / transport error. Retried like a 5xx; REPLACE
                    // makes a re-send idempotent (spec 11.1).
                    result.Code = 599;
                    result.Body = "Transport: " + ex.Message;
                    waitMs = attempt < RETRY_WAITS_MS.Length ? RETRY_WAITS_MS[attempt] : -1;
                }

                if (waitMs < 0) break;
                await Task.Delay(waitMs);
            }

            result.LatencyMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string Snip(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > 500 ? s.Substring(0, 500) : s;
        }

        /// <summary>
        /// appSettings first (Web.config pulls these from an uncommitted
        /// secrets.config), then a machine environment variable as fallback.
        /// deploy-iis.yml force-copies the repo Web.config over the box on
        /// every deploy, so the value must never live in Web.config itself.
        /// </summary>
        private static string ReadSetting(string key)
        {
            string v = ConfigurationManager.AppSettings[key];
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            v = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        private List<string> ResolveWerks(RfcDestination dest, string werksParam)
        {
            if (!string.IsNullOrWhiteSpace(werksParam) &&
                !werksParam.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return werksParam.Split(',')
                    .Select(w => w.Trim().ToUpperInvariant())
                    .Where(w => w.Length > 0 && w.Length <= 4)
                    .Distinct()
                    .ToList();
            }

            // Retail stores only — VLFKZ='A' excludes DC/HUB (VLFKZ='B').
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
            var list = new List<string>();
            foreach (IRfcStructure row in data)
            {
                string wa = row.GetString("WA") ?? "";
                string w = wa.Length >= 4 ? wa.Substring(0, 4).Trim() : wa.Trim();
                if (w.Length > 0) list.Add(w);
            }
            return list.Distinct().OrderBy(w => w).ToList();
        }

        private bool TryResolveEnv(string env, out RfcConfigParameters rfcPar)
        {
            rfcPar = null;
            switch ((env ?? "dev").Trim().ToLowerInvariant())
            {
                case "qa":
                case "quality":
                    rfcPar = BaseController.rfcConfigparametersquality();
                    return true;
                case "prod":
                case "production":
                    rfcPar = BaseController.rfcConfigparametersproduction();
                    return true;
                case "dev":
                case "development":
                case "":
                    rfcPar = BaseController.rfcConfigparameters();
                    return true;
                default:
                    return false;
            }
        }
    }
}
