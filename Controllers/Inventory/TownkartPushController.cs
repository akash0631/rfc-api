using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
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
    /// FM: Z_INV_STOREWISE_V2 (FG ZINV_UT_API1, TR S4DK928230, DEV 2026-08-20).
    ///     SUM(MARD.LABST) by MATNR+WERKS over LGORT 0001+0002, MTART
    ///     restricted to APP/GM/FBG — exactly the selection in spec section 6 —
    ///     plus deletion-flag exclusion and zero-quantity support.
    ///     Z_INV_STOREWISE_V1 (FG ZINV_RFC11) is the Green On reader and stays
    ///     available via ?fm=. V1 hard-floors IV_MIN_QTY to 1 and so can never
    ///     emit qty 0. At minQty=1 the two agree exactly (DH24: 422 = 422).
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
    /// the extract keeps its previous quantity and stays orderable.
    ///
    /// minQty=-1 lifts the floor so zero-stock rows come through, but that is
    /// a DIAGNOSTIC, not the production path. MARD keeps a row per material
    /// and storage location for as long as a site has existed, so a no-floor
    /// read of QA store HA10 returned 1,544,211 rows / 85 MB (1,518,255 of
    /// them zeros) against 25,956 with the floor — 3,089 batches for a single
    /// store, and roughly 1.7M vendor calls across the estate. DEV showed
    /// 8,510 rows for the same query and hid the problem entirely.
    ///
    /// The production answer is /api/inv/townkart-delta, which bounds the work
    /// by MSEG movement rather than by MARD retention. See that method.
    ///
    /// Endpoints:
    ///   POST /api/inv/townkart-push   — full snapshot (bootstrap)
    ///     ?env=dev|qa|prod       (default dev — FM currently exists on DEV only)
    ///     &amp;werks=HA10,HA11|all    (default HA10 — "all" needs confirm, see below)
    ///     &amp;dryRun=true|false     (default TRUE — builds payload, no vendor call)
    ///     &amp;businessDate=2026-08-20 (default: server date, IST)
    ///     &amp;chunkSize=500          (records per POST, spec section 5)
    ///     &amp;minQty=1               (-1 = no floor, emits sold-out SKUs as 0)
    ///     &amp;seg=APP,GM,FBG         (MTART segments)
    ///     &amp;fm=Z_INV_STOREWISE_V2  (whitelisted reader FM)
    ///     &amp;confirm=ALL_STORES     (required to live-push werks=all)
    ///
    ///   POST /api/inv/townkart-delta  — movement delta (steady state)
    ///     ?env=dev|qa|prod
    ///     &amp;dryRun=true|false     (default TRUE)
    ///     &amp;werks=HA10            (optional; omit = every site that moved)
    ///     &amp;windowMinutes=60      (max span consumed per call)
    ///     &amp;maxPairs=20000        (safety cap; halves the window if hit)
    ///     &amp;seedFrom=...          (one-off: create the watermark, no push)
    ///
    ///   GET  /api/inv/townkart-state  — watermark age, credentials, policy
    ///     ?env=dev|qa|prod
    ///     &amp;staleMinutes=180      (age above which the verdict is STALE)
    ///
    ///   Both push endpoints also take, per spec 8.3 / vendor question Q1:
    ///     &amp;zeroMode=send|skip        (default send; TOWNKART_ZERO_MODE)
    ///     &amp;zeroAdjustment=REPLACE    (adjustmentType for qty 0 rows;
    ///                                    TOWNKART_ZERO_ADJUSTMENT)
    ///
    /// Auth (ours): X-RFC-Key: v2-rfc-proxy-2026
    /// Auth (theirs): the two endpoints do NOT share a scheme. Their Postman
    ///   collection sends sku-inventory with X-Api-Key + X-Api-Token and no
    ///   Authorization header, and sync-inventory-from-sap with Authorization
    ///   Bearer + a context_shopId cookie and neither X-Api-*. The two bearers
    ///   are different tokens. Config keys: TOWNKART_API_KEY (X-Api-Key),
    ///   TOWNKART_TOKEN (X-Api-Token), TOWNKART_SYNC_TOKEN, TOWNKART_SYNC_COOKIE.
    ///   Never hardcode them —
    ///   this repo is public. Web.config carries appSettings file="secrets.config"
    ///   on this branch, but that wiring only reaches the host on a deploy, so
    ///   machine environment variables are the path that works today and needs
    ///   no deploy at all. ReadSetting checks appSettings first, then Machine.
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

        // FG ZINV_UT_DLT1, TR S4DK928232, DEV 2026-08-20. Stateless: the
        // caller passes the window and owns the watermark.
        private const string DELTA_FM = "Z_INV_DELTA_UT_V1";

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
            string fm = "Z_INV_STOREWISE_V2",
            string confirm = null,
            bool sync = false,
            string zeroMode = null,
            string zeroAdjustment = null)
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
                              "TOWNKART_API_KEY is the opaque 43-char X-Api-Key; TOWNKART_TOKEN " +
                              "is the JWT sent as X-Api-Token. Set them as machine environment " +
                              "variables (works with no deploy) or in secrets.config alongside " +
                              "Web.config. Never commit them — this repo is public."
                });
            }

            ZeroPolicy zeroPolicy = ZeroPolicy.Resolve(zeroMode, zeroAdjustment);

            var siteResults = new List<JObject>();
            int grandSkus = 0, grandPushed = 0, grandFailed = 0;
            int grandClamped = 0, grandBatches = 0, grandSapNeg = 0;
            int grandZeros = 0, grandZerosSkipped = 0;
            int bodyJudged = 0, bodyUnjudged = 0;
            bool abortRun = false;
            string abortReason = null;
            object samplePayload = null;

            var invHeaders = InventoryHeaders(apiKey, bearer);

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SEC) })
            {
                // Spec section 13: sites run one after another, and a failure on
                // one site must not stop the rest.
                foreach (string site in werksList)
                {
                    if (abortRun) break;

                    var siteStart = DateTime.Now;
                    JArray rows;
                    int fmCount, sapNeg;
                    try
                    {
                        rows = ReadStock(dest, fmName, site, minQty, seg,
                                         out fmCount, out sapNeg);
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

                    int clamped, zeros, zerosSkipped;
                    JArray items = MapItems(rows, site, zeroPolicy,
                                            out clamped, out zeros, out zerosSkipped);
                    grandSkus += items.Count;
                    grandClamped += clamped;
                    grandSapNeg += sapNeg;
                    grandZeros += zeros;
                    grandZerosSkipped += zerosSkipped;

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
                            ["zero_qty"] = zeros,
                            ["zeros_skipped"] = zerosSkipped,
                            ["negatives_clamped_in_sap"] = sapNeg,
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
                            http, TOWNKART_URL, payload.ToString(Formatting.None), invHeaders);

                        if (outcome.BodyOk.HasValue) bodyJudged++; else bodyUnjudged++;

                        batchLog.Add(new JObject
                        {
                            ["batch"] = (off / chunkSize) + 1,
                            ["skus"] = take,
                            ["http_code"] = outcome.Code,
                            ["ok"] = outcome.Ok,
                            ["body_verdict"] = outcome.BodyOk.HasValue
                                ? (outcome.BodyOk.Value ? "accepted" : "rejected")
                                : "unrecognised",
                            ["body_note"] = outcome.BodyNote,
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
                        ["zero_qty"] = zeros,
                        ["zeros_skipped"] = zerosSkipped,
                        ["negatives_clamped_in_sap"] = sapNeg,
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
                    syncResult = await RunSync(http);
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
                    NegativesClampedInSap = grandSapNeg,
                    NegativesClamped = grandClamped,
                    Batches = grandBatches,
                    Pushed = grandPushed,
                    Failed = grandFailed,
                    ZeroQtySkus = grandZeros,
                    ZerosSkipped = grandZerosSkipped,
                    ZeroPolicy = zeroPolicy.Describe(),
                    BodyVerdict = BodyVerdictNote(bodyJudged, bodyUnjudged),
                    ZeroQtyCaveat = minQty > 0
                        ? "minQty=" + minQty + " — sold-out SKUs are omitted, so Townkart " +
                          "keeps their previous quantity (spec 8.3 Option 2). This is the " +
                          "correct shape for a bootstrap: follow it with /api/inv/" +
                          "townkart-delta seeded at this run's start time, which sends the " +
                          "zeros as they happen. Do NOT use minQty=-1 in production — it " +
                          "emits every MARD row ever created (QA HA10: 1,544,211 rows)."
                        : "minQty=" + minQty + " — no floor. Diagnostic only: this returns a " +
                          "zero for every MARD row the site has ever had, not just the SKUs " +
                          "that changed. Use /api/inv/townkart-delta instead.",
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

        // ── Delta push (spec 8.3 Option 1) ────────────────────────────────
        //
        // The full push can only ever say "here is what has stock". Under
        // REPLACE semantics that leaves a sold-out SKU sitting at its old
        // quantity forever. The fix is NOT to lift the min-qty floor: MARD
        // keeps a row per material/storage-location for as long as the site
        // has existed, so a no-floor read of one QA store returned 1,544,211
        // rows / 85 MB — 3,089 batches for a single store.
        //
        // Z_INV_DELTA_UT_V1 bounds the work by MSEG movement instead. A SKU
        // that sells out has a movement, so it comes back with quantity 0 and
        // Townkart is told to zero it. The watermark is just two scalars, so
        // it lives here rather than in a DDIC table, and it only moves after
        // the vendor has confirmed every batch.

        private static readonly SemaphoreSlim DeltaGate = new SemaphoreSlim(1, 1);

        [HttpPost]
        [Route("townkart-delta")]
        public async Task<IHttpActionResult> Delta(
            string env = "dev",
            bool dryRun = true,
            string werks = null,
            string businessDate = null,
            int chunkSize = 500,
            string seg = "APP,GM,FBG",
            int windowMinutes = 60,
            int maxPairs = 20000,
            string seedFrom = null,
            bool sync = false,
            string zeroMode = null,
            string zeroAdjustment = null)
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

            if (chunkSize < 1 || chunkSize > 5000)
            {
                return Json(new { Status = false, Message = "chunkSize must be 1..5000" });
            }
            if (windowMinutes < 1 || windowMinutes > 1440)
            {
                return Json(new { Status = false, Message = "windowMinutes must be 1..1440" });
            }

            RfcConfigParameters rfcPar;
            if (!TryResolveEnv(env, out rfcPar))
            {
                return Json(new { Status = false, Message = "Invalid env '" + env + "'" });
            }

            string channel = "TOWNKART_" + env.Trim().ToUpperInvariant();

            // Seeding is deliberately a separate, explicit call. It is the
            // only way a watermark comes into existence.
            if (!string.IsNullOrWhiteSpace(seedFrom))
            {
                DateTime seeded;
                if (!TryParseStamp(seedFrom, out seeded))
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "seedFrom must be 'yyyy-MM-dd HH:mm:ss' or 'yyyyMMddHHmmss'"
                    });
                }
                SaveWatermark(channel, seeded, 0, "seeded");
                return Json(new
                {
                    Status = true,
                    Message = "Watermark seeded. Run the full push for the same moment " +
                              "first, or Townkart keeps whatever it already holds for " +
                              "SKUs that never move again.",
                    Channel = channel,
                    WatermarkAt = seeded.ToString("yyyy-MM-dd HH:mm:ss"),
                    StateFile = WatermarkPath(channel)
                });
            }

            DateTime from;
            if (!TryLoadWatermark(channel, out from))
            {
                // Absent state must never be read as "since the beginning of
                // time" — that would replay every movement SAP has ever kept.
                return Json(new
                {
                    Status = false,
                    Message = "No watermark for channel " + channel + ". Bootstrap first: " +
                              "run /api/inv/townkart-push (full, minQty=1) for every site, " +
                              "then POST this endpoint with &seedFrom=<the moment that run " +
                              "started> to start the delta from there.",
                    Channel = channel,
                    StateFile = WatermarkPath(channel)
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
                              "TOWNKART_API_KEY is the opaque 43-char X-Api-Key; TOWNKART_TOKEN " +
                              "is the JWT sent as X-Api-Token. Set them as machine environment " +
                              "variables (works with no deploy) or in secrets.config alongside " +
                              "Web.config. Never commit them — this repo is public."
                });
            }

            // Two overlapping runs would double-push and race the watermark
            // backwards. A cron that fires faster than a run completes is the
            // normal way that happens.
            if (!await DeltaGate.WaitAsync(0))
            {
                return Json(new
                {
                    Status = false,
                    Message = "A delta run is already in progress on this host. Skipped."
                });
            }

            try
            {
                RfcDestination dest;
                try
                {
                    dest = RfcDestinationManager.GetDestination(rfcPar);
                }
                catch (Exception ex)
                {
                    return Json(new { Status = false, Message = "RFC connect failed: " + ex.Message });
                }

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

                DateTime now = DateTime.Now;
                if (from > now)
                {
                    return Json(new
                    {
                        Status = false,
                        Message = "Watermark " + from.ToString("yyyy-MM-dd HH:mm:ss") +
                                  " is in the future. Re-seed it.",
                        Channel = channel
                    });
                }

                DateTime to = from.AddMinutes(windowMinutes);
                bool caughtUp = to >= now;
                if (caughtUp) to = now;

                // A dense burst inside the window would silently lose rows to
                // the UP TO n ROWS cap, so the FM reports truncation and the
                // window is halved until it fits.
                JArray rows;
                int pairs = 0, zeroCount = 0, sapNeg = 0, narrowed = 0;
                string fmMessage;
                while (true)
                {
                    bool truncated;
                    try
                    {
                        rows = ReadDelta(dest, from, to, werks, seg, maxPairs,
                                         out pairs, out truncated, out zeroCount,
                                         out sapNeg, out fmMessage);
                    }
                    catch (Exception ex)
                    {
                        return Json(new
                        {
                            Status = false,
                            Message = "Z_INV_DELTA_UT_V1 failed: " + ex.Message,
                            Channel = channel,
                            WindowFrom = from.ToString("yyyy-MM-dd HH:mm:ss"),
                            WindowTo = to.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }

                    if (!truncated) break;

                    double span = (to - from).TotalSeconds;
                    if (span <= 60)
                    {
                        return Json(new
                        {
                            Status = false,
                            Message = "More than " + maxPairs + " changed SKU/site pairs in a " +
                                      "single minute. Raise &maxPairs or bootstrap this site " +
                                      "with a full push — the watermark was NOT advanced.",
                            Channel = channel,
                            WindowFrom = from.ToString("yyyy-MM-dd HH:mm:ss"),
                            WindowTo = to.ToString("yyyy-MM-dd HH:mm:ss"),
                            Pairs = pairs
                        });
                    }
                    to = from.AddSeconds(Math.Floor(span / 2));
                    caughtUp = false;
                    narrowed++;
                }

                // One payload per store: storeCode is a top-level field, so a
                // multi-site delta has to be split before it can be sent.
                var bySite = new Dictionary<string, JArray>(StringComparer.OrdinalIgnoreCase);
                foreach (JObject row in rows.OfType<JObject>())
                {
                    string s = (string)row["store_code"];
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    if (!bySite.ContainsKey(s)) bySite[s] = new JArray();
                    bySite[s].Add(row);
                }

                ZeroPolicy zeroPolicy = ZeroPolicy.Resolve(zeroMode, zeroAdjustment);

                var siteResults = new List<JObject>();
                int grandSkus = 0, grandZeros = 0, grandPushed = 0, grandFailed = 0;
                int grandClamped = 0, grandBatches = 0, grandZerosSkipped = 0;
                int bodyJudged = 0, bodyUnjudged = 0;
                bool abortRun = false;
                string abortReason = null;
                object samplePayload = null;

                var invHeaders = InventoryHeaders(apiKey, bearer);

                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SEC) })
                {
                    foreach (var kv in bySite.OrderBy(k => k.Key))
                    {
                        if (abortRun) break;

                        var siteStart = DateTime.Now;
                        string site = kv.Key;

                        int clamped, zeros, zerosSkipped;
                        JArray items = MapItems(kv.Value, site, zeroPolicy,
                                                out clamped, out zeros, out zerosSkipped);
                        grandSkus += items.Count;
                        grandZeros += zeros;
                        grandZerosSkipped += zerosSkipped;
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
                                ["status"] = items.Count == 0 ? "No changes" : "Dry run",
                                ["skus"] = items.Count,
                                ["zero_qty"] = zeros,
                                ["zeros_skipped"] = zerosSkipped,
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
                                http, TOWNKART_URL, payload.ToString(Formatting.None), invHeaders);

                            if (outcome.BodyOk.HasValue) bodyJudged++; else bodyUnjudged++;

                            batchLog.Add(new JObject
                            {
                                ["batch"] = (off / chunkSize) + 1,
                                ["skus"] = take,
                                ["http_code"] = outcome.Code,
                                ["ok"] = outcome.Ok,
                                ["body_verdict"] = outcome.BodyOk.HasValue
                                    ? (outcome.BodyOk.Value ? "accepted" : "rejected")
                                    : "unrecognised",
                                ["body_note"] = outcome.BodyNote,
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
                            ["skus"] = items.Count,
                            ["zero_qty"] = zeros,
                            ["zeros_skipped"] = zerosSkipped,
                            ["negatives_clamped"] = clamped,
                            ["batches_sent"] = okBatches + failBatches,
                            ["batches_ok"] = okBatches,
                            ["batches_failed"] = failBatches,
                            ["duration_ms"] = (int)(DateTime.Now - siteStart).TotalMilliseconds,
                            ["batch_log"] = batchLog
                        });
                    }

                    JObject syncResult = null;
                    if (sync && !dryRun && !abortRun && grandPushed > 0)
                    {
                        syncResult = await RunSync(http);
                    }

                    // The whole point of holding the watermark here: it moves
                    // only when Townkart has actually taken every row. A dry
                    // run proves nothing was delivered, so it must not move
                    // either.
                    bool advanced = false;
                    string advanceNote;
                    if (dryRun)
                    {
                        advanceNote = "Dry run — watermark left at " +
                                      from.ToString("yyyy-MM-dd HH:mm:ss") + ".";
                    }
                    else if (abortRun || grandFailed > 0)
                    {
                        advanceNote = "Failures in this window — watermark NOT advanced, " +
                                      "the same window will be retried next run.";
                    }
                    else
                    {
                        SaveWatermark(channel, to, grandPushed, "delta pushed");
                        advanced = true;
                        advanceNote = caughtUp
                            ? "Watermark advanced to " + to.ToString("yyyy-MM-dd HH:mm:ss") +
                              " — caught up."
                            : "Watermark advanced to " + to.ToString("yyyy-MM-dd HH:mm:ss") +
                              " — still behind, call again to continue catching up.";
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
                        Fm = DELTA_FM,
                        DryRun = dryRun,
                        Channel = channel,
                        BusinessDate = bizDate,
                        Seg = seg,
                        WindowFrom = from.ToString("yyyy-MM-dd HH:mm:ss"),
                        WindowTo = to.ToString("yyyy-MM-dd HH:mm:ss"),
                        WindowNarrowedTimes = narrowed,
                        CaughtUp = caughtUp,
                        WatermarkAdvanced = advanced,
                        WatermarkNote = advanceNote,
                        ChangedPairs = pairs,
                        SitesTouched = bySite.Count,
                        TotalSkus = grandSkus,
                        ZeroQtySkus = grandZeros,
                        NegativesClampedInSap = sapNeg,
                        NegativesClamped = grandClamped,
                        Batches = grandBatches,
                        Pushed = grandPushed,
                        Failed = grandFailed,
                        ZerosSkipped = grandZerosSkipped,
                        ZeroPolicy = zeroPolicy.Describe(),
                        BodyVerdict = BodyVerdictNote(bodyJudged, bodyUnjudged),
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
            finally
            {
                DeltaGate.Release();
            }
        }

        // ── Health / state (read-only) ────────────────────────────────────
        //
        // Everything this integration depends on that is NOT in the repo:
        // the watermark file, the credentials, and the zero policy. A cron
        // that quietly stops advancing the watermark looks identical to a
        // quiet estate from the outside, so the age is the signal to alert on.

        [HttpGet]
        [Route("townkart-state")]
        public IHttpActionResult State(string env = "dev", int staleMinutes = 180)
        {
            IEnumerable<string> keyHeaders;
            bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
            if (!hasKey || keyHeaders.FirstOrDefault() != API_KEY)
            {
                return Json(new { Status = false, Message = "Unauthorized" });
            }

            string channel = "TOWNKART_" + (env ?? "dev").Trim().ToUpperInvariant();
            string dir = StateDir();
            string path = WatermarkPath(channel);

            bool dirExists = Directory.Exists(dir);
            bool dirWritable = false;
            string dirNote = null;
            if (dirExists)
            {
                try
                {
                    string probe = Path.Combine(dir, ".writeprobe");
                    File.WriteAllText(probe, "");
                    File.Delete(probe);
                    dirWritable = true;
                }
                catch (Exception ex)
                {
                    dirNote = "Not writable by the app pool identity: " + ex.Message;
                }
            }
            else
            {
                dirNote = "Does not exist. It is created on first seed, but the app pool " +
                          "identity must be able to create it.";
            }

            DateTime at;
            bool haveWm = TryLoadWatermark(channel, out at);
            double ageMin = haveWm ? (DateTime.Now - at).TotalMinutes : 0;

            // Raw file too, so a corrupt watermark is visible rather than just
            // reported as absent.
            string raw = null;
            if (File.Exists(path))
            {
                try { raw = Snip(File.ReadAllText(path)); }
                catch (Exception ex) { raw = "unreadable: " + ex.Message; }
            }

            string bearer = ReadSetting("TOWNKART_TOKEN");
            string apiKey = ReadSetting("TOWNKART_API_KEY");

            ZeroPolicy zero = ZeroPolicy.Resolve(null, null);

            string verdict =
                !haveWm ? (raw == null ? "NOT SEEDED" : "WATERMARK UNREADABLE")
                : ageMin > staleMinutes ? "STALE"
                : "OK";

            return Json(new
            {
                Status = verdict == "OK",
                Verdict = verdict,
                Env = env,
                Channel = channel,
                StateDir = dir,
                StateDirExists = dirExists,
                StateDirWritable = dirWritable,
                StateDirNote = dirNote,
                StateFile = path,
                StateFileExists = File.Exists(path),
                WatermarkAt = haveWm ? at.ToString("yyyy-MM-dd HH:mm:ss") : null,
                WatermarkAgeMinutes = haveWm ? (int?)Math.Round(ageMin) : null,
                StaleAfterMinutes = staleMinutes,
                RawState = raw,
                CredentialsConfigured =
                    !string.IsNullOrWhiteSpace(bearer) && !string.IsNullOrWhiteSpace(apiKey),
                // Lengths only. Their sample X-Api-Key is 43 chars and the
                // X-Api-Token JWT is 320, so a wrong-way-round pair is visible
                // here without printing either secret.
                TokenLength = string.IsNullOrWhiteSpace(bearer) ? 0 : bearer.Length,
                ApiKeyLength = string.IsNullOrWhiteSpace(apiKey) ? 0 : apiKey.Length,
                SyncTokenConfigured = !string.IsNullOrWhiteSpace(ReadSetting("TOWNKART_SYNC_TOKEN")),
                SyncCookieConfigured = !string.IsNullOrWhiteSpace(ReadSetting("TOWNKART_SYNC_COOKIE")),
                ZeroPolicy = zero.Describe(),
                ReaderFm = "Z_INV_STOREWISE_V2",
                DeltaFm = DELTA_FM,
                TownkartUrl = TOWNKART_URL,
                ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        // ── Watermark state (host-local, survives deploys) ────────────────
        //
        // deploy-iis.yml force-copies the repo Web.config over the host's on
        // every deploy, so anything under the site root is build output, not
        // host state. The watermark lives outside it.

        private static string StateDir()
        {
            string dir = ReadSetting("TOWNKART_STATE_DIR");
            return string.IsNullOrWhiteSpace(dir) ? @"C:\V2RfcState" : dir.Trim();
        }

        private static string WatermarkPath(string channel)
        {
            return Path.Combine(StateDir(), "watermark_" + channel + ".json");
        }

        private static bool TryLoadWatermark(string channel, out DateTime at)
        {
            at = default(DateTime);
            string path = WatermarkPath(channel);
            if (!File.Exists(path)) return false;
            try
            {
                JObject o = JObject.Parse(File.ReadAllText(path));
                return TryParseStamp((string)o["at"], out at);
            }
            catch (Exception)
            {
                // A corrupt state file must fail closed. Silently restarting
                // from now would skip every movement since the last good run.
                return false;
            }
        }

        private static void SaveWatermark(string channel, DateTime at, int count, string status)
        {
            string dir = StateDir();
            Directory.CreateDirectory(dir);

            var o = new JObject
            {
                ["channel"] = channel,
                ["at"] = at.ToString("yyyyMMddHHmmss"),
                ["at_readable"] = at.ToString("yyyy-MM-dd HH:mm:ss"),
                ["last_count"] = count,
                ["last_status"] = status,
                ["updated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Write-then-move: a half-written watermark reads as corrupt and
            // fails the next run closed, which is worse than a stale one.
            string path = WatermarkPath(channel);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, o.ToString(Formatting.Indented));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        private static bool TryParseStamp(string s, out DateTime at)
        {
            at = default(DateTime);
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            string[] formats =
            {
                "yyyyMMddHHmmss", "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm"
            };
            return DateTime.TryParseExact(s, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out at);
        }

        // ── SAP read ──────────────────────────────────────────────────────

        private JArray ReadDelta(RfcDestination dest, DateTime from, DateTime to,
                                 string werks, string seg, int maxPairs,
                                 out int pairs, out bool truncated, out int zeroCount,
                                 out int sapNeg, out string fmMessage)
        {
            IRfcFunction fn = dest.Repository.CreateFunction(DELTA_FM);
            fn.SetValue("IV_FROM_DT", from.ToString("yyyyMMdd"));
            fn.SetValue("IV_FROM_TM", from.ToString("HHmmss"));
            fn.SetValue("IV_TO_DT", to.ToString("yyyyMMdd"));
            fn.SetValue("IV_TO_TM", to.ToString("HHmmss"));
            fn.SetValue("IV_MAX_PAIRS", maxPairs);
            if (!string.IsNullOrWhiteSpace(werks)) fn.SetValue("IV_WERKS_CSV", werks);
            if (!string.IsNullOrWhiteSpace(seg)) fn.SetValue("IV_SEG_CSV", seg);
            fn.Invoke(dest);

            pairs = fn.GetInt("EV_PAIRS");
            zeroCount = fn.GetInt("EV_ZERO_COUNT");
            sapNeg = fn.GetInt("EV_NEG_COUNT");
            truncated = (fn.GetString("EV_TRUNC") ?? "").Trim() == "X";
            fmMessage = fn.GetString("EV_MESSAGE");

            string json = fn.GetString("EV_JSON");
            if (string.IsNullOrWhiteSpace(json) || json.Length <= 2) return new JArray();
            return JArray.Parse(json);
        }

        private JArray ReadStock(RfcDestination dest, string fmName, string site,
                                 int minQty, string seg, out int fmCount, out int sapNeg)
        {
            IRfcFunction fn = dest.Repository.CreateFunction(fmName);
            fn.SetValue("IV_WERKS_CSV", site);
            fn.SetValue("IV_MIN_QTY", minQty);
            if (!string.IsNullOrWhiteSpace(seg)) fn.SetValue("IV_SEG_CSV", seg);
            fn.Invoke(dest);

            fmCount = fn.GetInt("EV_COUNT");

            // V2 clamps negative aggregates to 0 at source and reports the
            // count. V1 has no such export, so ask only when it can exist.
            sapNeg = 0;
            if (fmName != "Z_INV_STOREWISE_V1")
            {
                try { sapNeg = fn.GetInt("EV_NEG_COUNT"); }
                catch (Exception) { sapNeg = 0; }
            }
            string json = fn.GetString("EV_JSON");
            if (string.IsNullOrWhiteSpace(json) || json.Length <= 2) return new JArray();
            return JArray.Parse(json);
        }

        // ── Mapping (spec section 8.2) ────────────────────────────────────

        /// <summary>
        /// How a zero-quantity row is expressed to Townkart. Vendor question
        /// Q1/O4 — does quantity 0 under REPLACE actually clear their stock, or
        /// do they need a different adjustmentType, or a separate de-list call?
        ///
        /// The reader FM does not change under any of those answers: it already
        /// emits an explicit 0. Only this mapping moves, so the answer is a
        /// setting rather than a rebuild.
        ///
        ///   zeroMode=send (default) — qty 0 goes as a normal REPLACE row
        ///   zeroMode=skip           — zero rows are dropped from the payload
        ///   zeroAdjustment=X        — zero rows carry adjustmentType X instead
        ///
        /// Defaults come from TOWNKART_ZERO_MODE / TOWNKART_ZERO_ADJUSTMENT so
        /// the answer can be applied on the host without a deploy.
        /// </summary>
        private class ZeroPolicy
        {
            public string Mode;
            public string Adjustment;

            public static ZeroPolicy Resolve(string mode, string adjustment)
            {
                string m = (mode ?? ReadSetting("TOWNKART_ZERO_MODE") ?? "send")
                           .Trim().ToLowerInvariant();
                if (m != "skip") m = "send";

                string a = adjustment ?? ReadSetting("TOWNKART_ZERO_ADJUSTMENT");
                a = string.IsNullOrWhiteSpace(a) ? ADJUSTMENT_TYPE : a.Trim();

                return new ZeroPolicy { Mode = m, Adjustment = a };
            }

            public bool Skip { get { return Mode == "skip"; } }

            public string Describe()
            {
                if (Skip)
                {
                    return "zeroMode=skip — sold-out SKUs are NOT sent, so Townkart keeps " +
                           "their previous quantity and they stay orderable. Only correct " +
                           "if Townkart clears stock some other way.";
                }
                return Adjustment == ADJUSTMENT_TYPE
                    ? "zeroMode=send — sold-out SKUs go as quantity 0 with adjustmentType " +
                      ADJUSTMENT_TYPE + ". Assumes 0 under REPLACE clears their stock (Q1/O4, " +
                      "unconfirmed)."
                    : "zeroMode=send — sold-out SKUs go as quantity 0 with adjustmentType " +
                      Adjustment + " (overridden from " + ADJUSTMENT_TYPE + ").";
            }
        }

        private JArray MapItems(JArray rows, string site, ZeroPolicy zero,
                                out int clamped, out int zeros, out int zerosSkipped)
        {
            clamped = 0;
            zeros = 0;
            zerosSkipped = 0;
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

                // Counted AFTER the clamp — a clamped negative is a zero we
                // send, and the count of zeros sent is the whole question.
                if (qty == 0)
                {
                    zeros++;
                    if (zero.Skip)
                    {
                        zerosSkipped++;
                        continue;
                    }
                }

                items.Add(new JObject
                {
                    ["itemSKU"] = sku,
                    ["quantity"] = qty,
                    ["shelfCode"] = SHELF_CODE,
                    ["inventoryType"] = INVENTORY_TYPE,
                    ["sla"] = SLA,
                    ["adjustmentType"] = qty == 0 ? zero.Adjustment : ADJUSTMENT_TYPE,
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

            /// <summary>
            /// What the response BODY said, independent of the status code.
            /// true = body confirms acceptance, false = body reports rejected
            /// records, null = shape not recognised.
            /// </summary>
            public bool? BodyOk;
            public string BodyNote;
        }

        /// <summary>
        /// A 2xx is not proof of acceptance. Spec section 12 shows a response
        /// envelope but never says whether a batch can be accepted at the HTTP
        /// layer while individual records inside it are rejected — that is
        /// vendor question Q5/O6, still unanswered.
        ///
        /// Until it is answered, this reads the body for any of the shapes a
        /// partial-accept API normally uses and fails the batch when it finds
        /// one. Failing a batch that actually succeeded costs one re-send, and
        /// REPLACE makes a re-send idempotent. Passing a batch that was
        /// actually rejected advances the watermark over it and loses those
        /// rows for good. The asymmetry decides the direction.
        ///
        /// Returns null when nothing is recognised, so an unknown envelope
        /// falls back to the status code rather than failing everything.
        /// </summary>
        private static bool? JudgeBody(string body, out string note)
        {
            note = null;
            if (string.IsNullOrWhiteSpace(body)) return null;

            JObject o;
            try
            {
                string t = body.TrimStart();
                if (t.Length == 0 || t[0] != '{') return null;
                o = JObject.Parse(body);
            }
            catch (Exception)
            {
                return null;
            }

            // 1. Explicit boolean success flag.
            string[] boolKeys = { "success", "isSuccess", "ok", "Status", "status" };
            foreach (string k in boolKeys)
            {
                JToken t = o[k];
                if (t == null) continue;
                if (t.Type == JTokenType.Boolean)
                {
                    if ((bool)t) return true;
                    note = k + "=false";
                    return false;
                }
                if (t.Type == JTokenType.String)
                {
                    string v = ((string)t ?? "").Trim();
                    if (v.Equals("error", StringComparison.OrdinalIgnoreCase) ||
                        v.Equals("failed", StringComparison.OrdinalIgnoreCase) ||
                        v.Equals("failure", StringComparison.OrdinalIgnoreCase) ||
                        v.Equals("rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        note = k + "=" + v;
                        return false;
                    }
                }
            }

            // 2. A non-empty list of rejected records.
            string[] listKeys =
            {
                "errors", "failedRecords", "failed", "rejected",
                "invalidRecords", "failures", "rejectedItems", "errorList"
            };
            foreach (string k in listKeys)
            {
                JArray a = o[k] as JArray;
                if (a != null && a.Count > 0)
                {
                    note = k + "[" + a.Count + "]";
                    return false;
                }
            }

            // 3. A non-zero rejection count.
            string[] countKeys =
            {
                "failedCount", "errorCount", "rejectedCount",
                "failureCount", "invalidCount"
            };
            foreach (string k in countKeys)
            {
                JToken t = o[k];
                if (t == null) continue;
                int n;
                if (int.TryParse(((t.Type == JTokenType.String) ? (string)t : t.ToString()),
                                 out n) && n > 0)
                {
                    note = k + "=" + n;
                    return false;
                }
            }

            return null;
        }

        /// <summary>
        /// The two Townkart endpoints do NOT share an auth scheme, so headers
        /// are set per request rather than on the HttpClient.
        ///
        /// Their own Postman collection (V2Store (3).json, 20-Aug-2026) sends:
        ///   sku-inventory            → X-Api-Key + X-Api-Token, no Authorization
        ///   sync-inventory-from-sap  → Authorization: Bearer + Cookie, and
        ///                              neither X-Api-Key nor X-Api-Token
        /// The two bearer values are different tokens (320 vs 323 chars), and
        /// X-Api-Key is an opaque 43-char string, not a JWT.
        ///
        /// An earlier revision put all three headers on the shared client, so
        /// the inventory call carried an Authorization header the vendor never
        /// asked for and the sync call carried the wrong credential and no
        /// cookie. Matching their working request exactly removes a whole class
        /// of 401 that would otherwise look like an expired token.
        /// </summary>
        /// <summary>
        /// sync-inventory-from-sap runs on its own credential — a different
        /// bearer plus a context_shopId cookie — so it fails closed rather than
        /// borrowing the inventory token and returning a 401 that reads like an
        /// expired key. Whether it is even required after a push is spec O7,
        /// still unanswered, which is why it stays opt-in.
        /// </summary>
        private async Task<JObject> RunSync(HttpClient http)
        {
            string syncBearer = ReadSetting("TOWNKART_SYNC_TOKEN");
            string syncCookie = ReadSetting("TOWNKART_SYNC_COOKIE");

            if (string.IsNullOrWhiteSpace(syncBearer))
            {
                return new JObject
                {
                    ["skipped"] = true,
                    ["reason"] = "TOWNKART_SYNC_TOKEN not configured. The sync endpoint uses a " +
                                 "different bearer and a context_shopId cookie from the " +
                                 "inventory endpoint — see their Postman collection. Sending " +
                                 "the inventory token here would just 401."
                };
            }

            BatchOutcome s = await PostWithRetry(
                http, TOWNKART_SYNC_URL, "{}", SyncHeaders(syncBearer, syncCookie));

            return new JObject
            {
                ["http_code"] = s.Code,
                ["ok"] = s.Ok,
                ["cookie_sent"] = !string.IsNullOrWhiteSpace(syncCookie),
                ["latency_ms"] = s.LatencyMs,
                ["response"] = Snip(s.Body)
            };
        }

        private static Dictionary<string, string> InventoryHeaders(string apiKey, string apiToken)
        {
            return new Dictionary<string, string>
            {
                { "X-Api-Key", apiKey },
                { "X-Api-Token", apiToken }
            };
        }

        private static Dictionary<string, string> SyncHeaders(string bearer, string cookie)
        {
            var h = new Dictionary<string, string>
            {
                { "Authorization", bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                                   ? bearer : "Bearer " + bearer }
            };
            if (!string.IsNullOrWhiteSpace(cookie)) h["Cookie"] = cookie;
            return h;
        }

        private async Task<BatchOutcome> PostWithRetry(HttpClient http, string url, string body,
                                                       IDictionary<string, string> headers)
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
                    var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                    if (headers != null)
                    {
                        foreach (var kv in headers)
                        {
                            if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                            req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                        }
                    }
                    HttpResponseMessage resp = await http.SendAsync(req);
                    result.Code = (int)resp.StatusCode;
                    result.Body = await resp.Content.ReadAsStringAsync();

                    if (resp.IsSuccessStatusCode)
                    {
                        string bodyNote;
                        result.BodyOk = JudgeBody(result.Body, out bodyNote);
                        result.BodyNote = bodyNote;

                        // A 2xx whose body reports rejected records is not an
                        // acceptance. Record-level rejection is deterministic,
                        // so it is not retried — it is surfaced, and it holds
                        // the watermark back.
                        result.Ok = result.BodyOk != false;
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

        /// <summary>
        /// Says out loud whether acceptance was decided by the body or only by
        /// the status code. "Every batch returned 200" is not the same claim as
        /// "every record was taken", and the difference is invisible unless it
        /// is printed.
        /// </summary>
        private static string BodyVerdictNote(int judged, int unjudged)
        {
            int total = judged + unjudged;
            if (total == 0) return null;

            if (judged == 0)
            {
                return "No batch response matched a known accept/reject envelope — " +
                       "acceptance was judged on the HTTP status code alone. If Townkart " +
                       "can reject individual records inside a 2xx (Q5/O6, unanswered), " +
                       "those rows are being counted as delivered and the watermark moves " +
                       "past them. Read batch_log[].response and teach JudgeBody the real " +
                       "shape before trusting a live run.";
            }
            if (unjudged > 0)
            {
                return judged + " of " + total + " batch responses were recognised at body " +
                       "level; the rest fell back to the HTTP status code.";
            }
            return "All " + total + " batch responses were recognised at body level.";
        }

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
