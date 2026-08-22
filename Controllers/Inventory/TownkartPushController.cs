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

        // FG ZINV_UT_MOV1, TR S4DK928276, DEV 2026-08-22.
        //
        // UT 2026-08-22: the 5 AM snapshot is absolute, but SAP cannot give
        // an intraday balance because POS sales only post at 11 PM. So the
        // intraday feed carries MOVEMENT, not balance, and Townkart applies
        // it to the running total Axapta is already decrementing.
        private const string MOVE_FM = "Z_INV_MOVE_UT_V1";

        // Sale movement types Axapta already reports, which must never be
        // sent again or the same piece is deducted twice.
        //
        // 251 POS sale and 252 its return are Axapta's. 601/602 — the
        // delivery goods issue and its reversal — are NOT: Axapta carries no
        // website sales, so the web channel reaches Townkart only through
        // this feed. Confirmed with Akash 2026-08-22. Everything unlisted is
        // carried, so a movement type nobody anticipated shows up as a
        // visible quantity rather than vanishing.
        private const string MOVE_EXCL_BWART = "251,252";

        // Additive semantics. REPLACE is wrong here by construction: the
        // whole point is "apply -2 to whatever you currently hold", which is
        // what UT described ("UT calculates 6 - 2 = 4"). Their spec v1.0
        // documents only REPLACE, so this value has to be one THEY implement
        // — it is a setting rather than a constant so their answer costs a
        // config change, not a rebuild.
        private const string MOVE_ADJUSTMENT = "ADD";

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

            // The instant each store's MARD was read, kept only for stores
            // whose snapshot actually reached Townkart. This becomes that
            // store's movement-feed start, so the additive feed picks up
            // exactly where the absolute one stopped — see SaveMoveMarks.
            var snapshotAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
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
                        // A live store with no stock is still a valid
                        // baseline — it was read, and the answer was zero.
                        // A dry run delivered nothing, so it seeds nothing.
                        if (!dryRun) snapshotAt[site] = siteStart;

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

                    // Only a store Townkart took in full gets a movement
                    // baseline. A partial store would start its additive
                    // feed from a total the vendor never actually holds.
                    if (failBatches == 0) snapshotAt[site] = siteStart;

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

                // Hand the movement feed its starting line. Merged rather
                // than replaced, so a single-store re-snapshot moves only
                // that store and leaves the rest of the estate alone.
                JObject moveSeed = null;
                if (snapshotAt.Count > 0)
                {
                    string moveChannel = "TOWNKART_" + (env ?? "dev").Trim().ToUpperInvariant();
                    Dictionary<string, DateTime> marks;
                    if (!TryLoadMoveMarks(moveChannel, out marks))
                    {
                        moveSeed = new JObject
                        {
                            ["seeded"] = false,
                            ["error"] = "Existing movement state file is unreadable; refusing to " +
                                        "overwrite it. Inspect " + MovePath(moveChannel) +
                                        " — the movement feed is additive, so a wrong baseline " +
                                        "drifts permanently."
                        };
                    }
                    else
                    {
                        foreach (var kv in snapshotAt) marks[kv.Key] = kv.Value;
                        SaveMoveMarks(moveChannel, marks);
                        moveSeed = new JObject
                        {
                            ["seeded"] = true,
                            ["stores_seeded"] = snapshotAt.Count,
                            ["stores_tracked"] = marks.Count,
                            ["state_file"] = MovePath(moveChannel),
                            ["note"] = "Each store's movement window starts at the instant its " +
                                       "own snapshot was read, not at a single run-wide cutoff."
                        };
                    }
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
                    MovementSeed = moveSeed,
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

        // ── Non-sale movement feed (UT 2026-08-22) ────────────────────────
        //
        // Townkart hold a running total: the 5 AM snapshot sets it, Axapta
        // decrements it as things sell. Everything else that moves stock in
        // a store — GRT, transfer out, damage, manual adjustment — is
        // invisible to them until the next morning. Their worked example:
        //
        //   05:00  SAP 10, sent to UT              UT 10
        //   day    2 sold POS + 2 sold web         UT  6   (SAP still 10)
        //   day    GRT of 2                        SAP 8,  UT still 6
        //   truth  4
        //
        // Sending SAP's 8 would be as wrong as leaving 6. Only "-2" is
        // right, because only UT knows what the sales already took off.
        //
        // NOTE ON THE TRANSPORT HALF: UT have asked for a separate API and
        // have not published it yet. The reader, the netting and the
        // per-store watermark are all determinate and are finished; the
        // endpoint URL and the field names are not. TOWNKART_MOVE_URL is
        // therefore unset by default and a live run refuses rather than
        // guessing at somebody else's contract. Dry runs work fully and
        // show the exact payload we intend to send.

        private static readonly SemaphoreSlim MoveGate = new SemaphoreSlim(1, 1);

        [HttpPost]
        [Route("townkart-movement")]
        public async Task<IHttpActionResult> Movement(
            string env = "dev",
            bool dryRun = true,
            string werks = null,
            string seg = "APP,GM,FBG",
            int chunkSize = 500,
            int maxRows = 50000,
            string exclBwart = null,
            string pool = null,
            string adjustment = null)
        {
            var runStart = DateTime.Now;

            IEnumerable<string> keyHeaders;
            bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
            if (!hasKey || keyHeaders.FirstOrDefault() != API_KEY)
                return Json(new { Status = false, Message = "Unauthorized — missing or invalid X-RFC-Key" });

            if (chunkSize < 1 || chunkSize > 5000)
                return Json(new { Status = false, Message = "chunkSize must be 1..5000" });

            RfcConfigParameters rfcPar;
            if (!TryResolveEnv(env, out rfcPar))
                return Json(new { Status = false, Message = "Invalid env '" + env + "'" });

            string channel = "TOWNKART_" + env.Trim().ToUpperInvariant();

            Dictionary<string, DateTime> marks;
            if (!TryLoadMoveMarks(channel, out marks))
                return Json(new
                {
                    Status = false,
                    Message = "Movement state file is unreadable. Refusing to run — this feed is " +
                              "additive, so guessing a baseline drifts permanently.",
                    StateFile = MovePath(channel)
                });

            if (marks.Count == 0)
                return Json(new
                {
                    Status = false,
                    Message = "No store has a movement baseline yet. Run /api/inv/townkart-push " +
                              "live first — it records each store's snapshot instant and seeds " +
                              "this feed from there.",
                    StateFile = MovePath(channel)
                });

            // A store is eligible only if its snapshot succeeded. Anything
            // the caller names that has no baseline is reported, not
            // silently skipped — a quietly absent store looks identical to
            // a store with nothing to send.
            var wanted = new List<string>();
            var noBaseline = new List<string>();
            if (!string.IsNullOrWhiteSpace(werks))
            {
                foreach (string w in werks.Split(',').Select(x => x.Trim().ToUpperInvariant())
                                          .Where(x => x.Length > 0).Distinct())
                {
                    if (marks.ContainsKey(w)) wanted.Add(w); else noBaseline.Add(w);
                }
            }
            else wanted.AddRange(marks.Keys);

            if (wanted.Count == 0)
                return Json(new
                {
                    Status = false,
                    Message = "None of the requested stores has a movement baseline.",
                    StoresWithoutBaseline = noBaseline
                });

            string moveUrl = ReadSetting("TOWNKART_MOVE_URL");
            if (!dryRun && string.IsNullOrWhiteSpace(moveUrl))
                return Json(new
                {
                    Status = false,
                    Message = "TOWNKART_MOVE_URL is not set. UT have asked for a separate " +
                              "movement API but have not published its address or payload " +
                              "shape yet, so there is nothing to POST to. Dry runs work and " +
                              "show exactly what we intend to send."
                });

            string bearer = ReadSetting("TOWNKART_TOKEN");
            string apiKey = ReadSetting("TOWNKART_API_KEY");
            if (!dryRun && (string.IsNullOrWhiteSpace(bearer) || string.IsNullOrWhiteSpace(apiKey)))
                return Json(new { Status = false, Message = "Townkart credentials not configured on this host." });

            if (!await MoveGate.WaitAsync(0))
                return Json(new { Status = false, Message = "A movement run is already in progress on this host. Skipped." });

            try
            {
                RfcDestination dest;
                try { dest = RfcDestinationManager.GetDestination(rfcPar); }
                catch (Exception ex)
                { return Json(new { Status = false, Message = "RFC connect failed: " + ex.Message }); }

                string adj = string.IsNullOrWhiteSpace(adjustment)
                    ? (ReadSetting("TOWNKART_MOVE_ADJUSTMENT") ?? MOVE_ADJUSTMENT)
                    : adjustment.Trim();
                string excl = string.IsNullOrWhiteSpace(exclBwart)
                    ? (ReadSetting("TOWNKART_MOVE_EXCL_BWART") ?? MOVE_EXCL_BWART)
                    : exclBwart.Trim();

                DateTime now = DateTime.Now;

                // Stores sharing a baseline share one FM call. After the
                // first run they all converge on the same "to", so the
                // fan-out collapses to a single call in steady state and is
                // only wide on the first pass after a snapshot.
                var groups = wanted.GroupBy(s => marks[s].ToString("yyyyMMddHHmmss"))
                                   .OrderBy(g => g.Key).ToList();

                var siteRows = new Dictionary<string, JArray>(StringComparer.OrdinalIgnoreCase);
                var groupLog = new JArray();
                int grandRows = 0, grandPairs = 0, grandNetZero = 0;

                foreach (var g in groups)
                {
                    DateTime from = marks[g.First()];
                    if (from > now)
                        return Json(new
                        {
                            Status = false,
                            Message = "Baseline " + from.ToString("yyyy-MM-dd HH:mm:ss") +
                                      " is in the future. Re-snapshot those stores.",
                            Stores = g.ToList()
                        });

                    string sites = string.Join(",", g);
                    JArray rows;
                    int rowCount, pairs, emitted, netZero, pos, neg;
                    bool truncated;
                    string fmMsg;
                    try
                    {
                        rows = ReadMovements(dest, from, now, sites, seg, maxRows, excl, pool,
                                             out rowCount, out pairs, out emitted, out netZero,
                                             out pos, out neg, out truncated, out fmMsg);
                    }
                    catch (Exception ex)
                    {
                        return Json(new
                        {
                            Status = false,
                            Message = MOVE_FM + " failed: " + ex.Message,
                            Stores = g.ToList(),
                            WindowFrom = from.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }

                    // Truncation here is not a performance note. A dropped
                    // movement is never re-derived, because nothing in this
                    // design re-reads a balance to correct it.
                    if (truncated)
                        return Json(new
                        {
                            Status = false,
                            Message = "More than " + maxRows + " movement rows in this window. " +
                                      "Raise &maxRows or run more often. Nothing was sent and no " +
                                      "baseline moved — an additive feed cannot recover a row it " +
                                      "never saw.",
                            Stores = g.ToList(),
                            WindowFrom = from.ToString("yyyy-MM-dd HH:mm:ss"),
                            WindowTo = now.ToString("yyyy-MM-dd HH:mm:ss")
                        });

                    grandRows += rowCount; grandPairs += pairs; grandNetZero += netZero;

                    groupLog.Add(new JObject
                    {
                        ["window_from"] = from.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["stores"] = new JArray(g.ToList()),
                        ["mseg_rows"] = rowCount,
                        ["pairs"] = pairs,
                        ["netted_to_zero"] = netZero,
                        ["emitted"] = emitted,
                        ["positive"] = pos,
                        ["negative"] = neg,
                        ["fm_message"] = fmMsg
                    });

                    foreach (JObject row in rows.OfType<JObject>())
                    {
                        string s = (string)row["store_code"];
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        if (!siteRows.ContainsKey(s)) siteRows[s] = new JArray();
                        siteRows[s].Add(row);
                    }
                }

                var siteResults = new List<JObject>();
                int grandSkus = 0, grandPushed = 0, grandFailed = 0, grandBatches = 0;
                int bodyJudged = 0, bodyUnjudged = 0;
                int ambiguousBatches = 0;
                bool abortRun = false;
                string abortReason = null;
                object samplePayload = null;
                var advanced = new List<string>();
                var ambiguousStores = new List<string>();
                var doubleApplyStores = new List<string>();

                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SEC) })
                {
                    var invHeaders = InventoryHeaders(apiKey, bearer);

                    foreach (string site in wanted.OrderBy(s => s))
                    {
                        if (abortRun) break;

                        JArray rows;
                        if (!siteRows.TryGetValue(site, out rows)) rows = new JArray();
                        JArray items = MapMovements(rows, site, adj);
                        grandSkus += items.Count;

                        if (samplePayload == null && items.Count > 0)
                            samplePayload = new
                            {
                                storeCode = site,
                                businessDate = runStart.ToString("yyyy-MM-dd"),
                                inventoryData = items.Take(2).ToList()
                            };

                        int siteBatches = (items.Count + chunkSize - 1) / chunkSize;
                        grandBatches += siteBatches;

                        if (items.Count == 0)
                        {
                            // Nothing moved. The baseline still advances —
                            // the window was genuinely consumed.
                            if (!dryRun) { marks[site] = now; advanced.Add(site); }
                            siteResults.Add(new JObject
                            {
                                ["store_code"] = site,
                                ["status"] = "No movements",
                                ["skus"] = 0
                            });
                            continue;
                        }

                        if (dryRun)
                        {
                            siteResults.Add(new JObject
                            {
                                ["store_code"] = site,
                                ["status"] = "Dry run",
                                ["skus"] = items.Count,
                                ["would_be_batches"] = siteBatches,
                                ["sample"] = new JArray(items.Take(3))
                            });
                            continue;
                        }

                        var batchLog = new JArray();
                        int okBatches = 0, failBatches = 0, skippedBatches = 0;

                        for (int off = 0; off < items.Count; off += chunkSize)
                        {
                            int take = Math.Min(chunkSize, items.Count - off);
                            var slice = new JArray();
                            for (int i = 0; i < take; i++) slice.Add(items[off + i]);

                            var payload = new JObject
                            {
                                ["storeCode"] = site,
                                ["businessDate"] = runStart.ToString("yyyy-MM-dd"),
                                ["inventoryData"] = slice
                            };

                            // retryAmbiguous:false — an additive quantity must
                            // never be re-sent on a maybe.
                            BatchOutcome outcome = await PostWithRetry(
                                http, moveUrl, payload.ToString(Formatting.None), invHeaders,
                                retryAmbiguous: false);

                            if (outcome.BodyOk.HasValue) bodyJudged++; else bodyUnjudged++;
                            if (outcome.Ambiguous) ambiguousBatches++;

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
                                ["latency_ms"] = outcome.LatencyMs,
                                ["ambiguous"] = outcome.Ambiguous,
                                ["response"] = Snip(outcome.Body)
                            });

                            if (outcome.Ok) { okBatches++; grandPushed += take; }
                            else
                            {
                                failBatches++; grandFailed += take;
                                if (outcome.Ambiguous && !ambiguousStores.Contains(site))
                                    ambiguousStores.Add(site);
                                if (outcome.Code == 401 || outcome.Code == 403)
                                {
                                    abortRun = true;
                                    abortReason = "HTTP " + outcome.Code + " from Townkart — token " +
                                                  "expired or invalid. Run aborted at site " + site + ".";
                                }

                                // Stop this store at the first failure. Every
                                // further batch we send widens the prefix that
                                // a re-run will double-apply (see below), and
                                // buys nothing: the baseline is already held.
                                int unsent = items.Count - (off + take);
                                grandFailed += unsent;
                                skippedBatches = (unsent + chunkSize - 1) / chunkSize;
                                break;
                            }
                        }

                        // Per store, and only on a clean sweep. A partial store
                        // keeps its old baseline so nothing is LOST — but that
                        // is not the same as being safe, and the REPLACE-era
                        // reasoning that used to sit here was wrong.
                        //
                        // Holding the baseline makes the next run re-read the
                        // same window and re-send the WHOLE store, including
                        // the batches that already returned 200. Under REPLACE
                        // that was free. Under ADD every one of those already-
                        // applied batches lands a second time. So a partial
                        // store is a guaranteed double-count on re-run, not a
                        // coin flip — the ambiguous case below is the coin
                        // flip, and this one is worse.
                        //
                        // It cannot be fixed by resuming from a prefix: the
                        // next window is [baseline, now'], a superset, so the
                        // item list differs and "skip the first N" is unsound.
                        // The batch loop therefore stops at the first failure
                        // to keep the applied prefix as small as possible, and
                        // the prefix is reported rather than hidden. A vendor
                        // idempotency key (Q17) removes the problem entirely.
                        bool siteAmbiguous = ambiguousStores.Contains(site);
                        bool doubleApplyRisk = okBatches > 0 && failBatches > 0;
                        if (doubleApplyRisk && !doubleApplyStores.Contains(site))
                            doubleApplyStores.Add(site);
                        if (failBatches == 0) { marks[site] = now; advanced.Add(site); }

                        siteResults.Add(new JObject
                        {
                            ["store_code"] = site,
                            ["status"] = failBatches == 0 ? "Success"
                                       : siteAmbiguous ? "UNKNOWN — may have applied"
                                       : (okBatches > 0 ? "Partially Successful" : "Failed"),
                            ["skus"] = items.Count,
                            ["batches_ok"] = okBatches,
                            ["batches_failed"] = failBatches,
                            ["batches_skipped"] = skippedBatches,
                            ["baseline_advanced"] = failBatches == 0,
                            ["needs_review"] = siteAmbiguous || doubleApplyRisk,
                            ["batches_already_applied"] = okBatches,
                            ["double_apply_warning"] = !doubleApplyRisk ? null
                                : okBatches + " batch(es) were accepted before this store failed. "
                                + "The baseline was held so nothing is lost, but re-running will "
                                + "send those accepted batches again and ADD their quantities a "
                                + "second time. Reconcile this store, or hold it out of the next "
                                + "run, until Townkart support an idempotency key.",
                            ["batch_log"] = batchLog
                        });
                    }

                    if (!dryRun && advanced.Count > 0) SaveMoveMarks(channel, marks);

                    return Json(new
                    {
                        Status = !abortRun && (dryRun || grandFailed == 0),
                        RunStatus = abortRun ? "Failed"
                                  : dryRun ? "Dry run"
                                  : grandFailed == 0 ? "Success"
                                  : (ambiguousBatches > 0 || doubleApplyStores.Count > 0)
                                        ? "UNKNOWN — manual reconciliation needed"
                                  : grandPushed > 0 ? "Partially Successful" : "Failed",
                        AbortReason = abortReason,
                        DoubleApplyStores = doubleApplyStores,
                        DoubleApplyNote = doubleApplyStores.Count == 0 ? null
                            : doubleApplyStores.Count + " store(s) had batches accepted before a "
                            + "later batch failed. Their baselines were held, so nothing is lost — "
                            + "but a plain re-run will ADD the accepted batches a second time. This "
                            + "is certain, not a risk. Reconcile or exclude those stores before the "
                            + "next run. An idempotency key from Townkart (Q17) removes it.",
                        AmbiguousBatches = ambiguousBatches,
                        AmbiguousStores = ambiguousStores,
                        AmbiguousNote = ambiguousBatches == 0 ? null
                            : ambiguousBatches + " batch(es) failed with a 5xx or a timeout and were "
                            + "NOT retried, because this feed is additive and a re-send would add the "
                            + "quantity twice. Townkart may or may not have applied them. The affected "
                            + "stores kept their old baseline, so the next run will re-send the same "
                            + "window — if Townkart did apply these batches, that will double-count. "
                            + "Reconcile these stores against Townkart before the next run, or ask "
                            + "Townkart for an idempotency key so the ambiguity stops existing.",
                        Env = env,
                        Fm = MOVE_FM,
                        DryRun = dryRun,
                        Channel = channel,
                        AdjustmentType = adj,
                        ExcludedMovementTypes = excl,
                        StoragePool = string.IsNullOrWhiteSpace(pool) ? "0001,0002" : pool,
                        WindowTo = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        StoresInScope = wanted.Count,
                        StoresWithoutBaseline = noBaseline,
                        BaselineGroups = groups.Count,
                        MsegRows = grandRows,
                        Pairs = grandPairs,
                        NettedToZero = grandNetZero,
                        TotalSkus = grandSkus,
                        Batches = grandBatches,
                        Pushed = grandPushed,
                        Failed = grandFailed,
                        BaselinesAdvanced = advanced.Count,
                        BodyVerdict = BodyVerdictNote(bodyJudged, bodyUnjudged),
                        MovementUrl = string.IsNullOrWhiteSpace(moveUrl)
                            ? "(TOWNKART_MOVE_URL unset — awaiting UT's movement API spec)"
                            : moveUrl,
                        SamplePayload = samplePayload,
                        StartedAt = runStart.ToString("yyyy-MM-dd HH:mm:ss"),
                        DurationMs = (int)(DateTime.Now - runStart).TotalMilliseconds,
                        Sites = siteResults
                    });
                }
            }
            finally { MoveGate.Release(); }
        }

        private JArray ReadMovements(RfcDestination dest, DateTime from, DateTime to,
                                     string werks, string seg, int maxRows, string excl, string pool,
                                     out int rows, out int pairs, out int emitted, out int netZero,
                                     out int pos, out int neg, out bool truncated, out string message)
        {
            IRfcFunction fn = dest.Repository.CreateFunction(MOVE_FM);
            fn.SetValue("IV_FROM_DT", from.ToString("yyyyMMdd"));
            fn.SetValue("IV_FROM_TM", from.ToString("HHmmss"));
            fn.SetValue("IV_TO_DT", to.ToString("yyyyMMdd"));
            fn.SetValue("IV_TO_TM", to.ToString("HHmmss"));
            fn.SetValue("IV_MAX_ROWS", maxRows);
            if (!string.IsNullOrWhiteSpace(werks)) fn.SetValue("IV_WERKS_CSV", werks);
            if (!string.IsNullOrWhiteSpace(seg)) fn.SetValue("IV_SEG_CSV", seg);
            if (!string.IsNullOrWhiteSpace(excl)) fn.SetValue("IV_EXCL_BWART", excl);
            if (!string.IsNullOrWhiteSpace(pool)) fn.SetValue("IV_POOL_CSV", pool);
            fn.Invoke(dest);

            rows = fn.GetInt("EV_ROWS");
            pairs = fn.GetInt("EV_PAIRS");
            emitted = fn.GetInt("EV_EMITTED");
            netZero = fn.GetInt("EV_NETZERO");
            pos = fn.GetInt("EV_POS_COUNT");
            neg = fn.GetInt("EV_NEG_COUNT");
            truncated = (fn.GetString("EV_TRUNC") ?? "").Trim() == "X";
            message = fn.GetString("EV_MESSAGE");

            string json = fn.GetString("EV_JSON");
            if (string.IsNullOrWhiteSpace(json) || json.Length <= 2) return new JArray();
            return JArray.Parse(json);
        }

        /// <summary>
        /// Movement rows are signed and MUST NOT be clamped. MapItems clamps
        /// negatives to zero because a negative BALANCE is nonsense; here a
        /// negative is the entire message, and clamping would silently
        /// delete every outbound movement while still reporting success.
        /// </summary>
        private JArray MapMovements(JArray rows, string site, string adjustment)
        {
            var items = new JArray();
            foreach (JObject row in rows.OfType<JObject>())
            {
                string sku = (string)row["sku"];
                if (string.IsNullOrWhiteSpace(sku)) continue;

                int qty = row["movement"] != null ? (int)row["movement"] : 0;
                if (qty == 0) continue;   // nothing to apply

                items.Add(new JObject
                {
                    ["itemSKU"] = sku,
                    ["quantity"] = qty,
                    ["shelfCode"] = SHELF_CODE,
                    ["inventoryType"] = INVENTORY_TYPE,
                    ["sla"] = SLA,
                    ["adjustmentType"] = adjustment,
                    ["remarks"] = GenericArticle(sku) + "-" + site,
                    ["facilityCode"] = FACILITY_CODE
                });
            }
            return items;
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
                Movement = MoveStateSummary(channel),
                ReaderFm = "Z_INV_STOREWISE_V2",
                DeltaFm = DELTA_FM,
                MoveFm = MOVE_FM,
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

        /// <summary>
        /// Movement feed health. The oldest baseline is the number that
        /// matters: a store nobody has swept is a store whose GRTs and
        /// transfers are piling up unsent, and from outside that looks
        /// exactly like a store where nothing happened.
        /// </summary>
        private static JObject MoveStateSummary(string channel)
        {
            Dictionary<string, DateTime> marks;
            if (!TryLoadMoveMarks(channel, out marks))
                return new JObject
                {
                    ["state"] = "UNREADABLE",
                    ["state_file"] = MovePath(channel),
                    ["note"] = "Additive feed — a wrong baseline drifts permanently. Inspect by hand."
                };

            if (marks.Count == 0)
                return new JObject
                {
                    ["state"] = "NOT SEEDED",
                    ["state_file"] = MovePath(channel),
                    ["note"] = "Run the full push live; it seeds each store from its own snapshot instant."
                };

            DateTime oldest = marks.Values.Min();
            DateTime newest = marks.Values.Max();
            return new JObject
            {
                ["state"] = "SEEDED",
                ["stores_tracked"] = marks.Count,
                ["oldest_baseline"] = oldest.ToString("yyyy-MM-dd HH:mm:ss"),
                ["oldest_age_minutes"] = (int)Math.Round((DateTime.Now - oldest).TotalMinutes),
                ["newest_baseline"] = newest.ToString("yyyy-MM-dd HH:mm:ss"),
                ["state_file"] = MovePath(channel),
                ["adjustment_type"] = ReadSetting("TOWNKART_MOVE_ADJUSTMENT") ?? MOVE_ADJUSTMENT,
                ["excluded_movement_types"] =
                    ReadSetting("TOWNKART_MOVE_EXCL_BWART") ?? MOVE_EXCL_BWART,
                ["movement_url"] = ReadSetting("TOWNKART_MOVE_URL") ??
                    "(unset — awaiting UT's movement API spec)"
            };
        }

        // ── Movement watermarks: one per store, not one per channel ───────
        //
        // The snapshot is absolute and the movement feed is additive, and
        // that combination makes the boundary between them unforgiving in
        // BOTH directions. Under REPLACE a re-send was free, so seeding to
        // the run's start was the safe choice — an overlap simply rewrote
        // the same number. Under ADD an overlap adds the same movement
        // twice, and a gap drops it; neither ever self-corrects, and both
        // accumulate every single day.
        //
        // A single cutoff for the whole estate cannot be right, because
        // each store's MARD is read at its own moment during a run that
        // takes minutes. So each store's movement window begins at the
        // instant ITS snapshot was read, and a store whose snapshot failed
        // gets no watermark at all rather than a wrong one.

        private static string MovePath(string channel)
        {
            return Path.Combine(StateDir(), "movement_" + channel + ".json");
        }

        /// <summary>
        /// Returns false only when the file exists but cannot be read. A
        /// missing file is "nothing seeded yet", which is a legitimate state
        /// and yields an empty map.
        /// </summary>
        private static bool TryLoadMoveMarks(string channel, out Dictionary<string, DateTime> marks)
        {
            marks = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            string path = MovePath(channel);
            if (!File.Exists(path)) return true;
            try
            {
                JObject o = JObject.Parse(File.ReadAllText(path));
                JObject stores = o["stores"] as JObject;
                if (stores == null) return true;
                foreach (var kv in stores)
                {
                    DateTime at;
                    if (TryParseStamp((string)kv.Value, out at)) marks[kv.Key] = at;
                }
                return true;
            }
            catch (Exception)
            {
                // Same reasoning as the snapshot watermark: silently
                // restarting from now would skip every movement since the
                // last good run, and under ADD that loss is permanent.
                marks = null;
                return false;
            }
        }

        private static void SaveMoveMarks(string channel, Dictionary<string, DateTime> marks)
        {
            string dir = StateDir();
            Directory.CreateDirectory(dir);

            var stores = new JObject();
            foreach (var kv in marks.OrderBy(k => k.Key))
                stores[kv.Key] = kv.Value.ToString("yyyyMMddHHmmss");

            var o = new JObject
            {
                ["channel"] = channel,
                ["stores"] = stores,
                ["store_count"] = marks.Count,
                ["updated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            string path = MovePath(channel);
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

            /// <summary>
            /// Set when the batch failed in a way that MIGHT still have been
            /// applied at the far end (5xx, or a transport timeout) and was
            /// deliberately not retried because the feed is additive. The
            /// quantity is in an unknown state: re-sending may double it,
            /// dropping it may lose it. Needs a human, not a retry.
            /// </summary>
            public bool Ambiguous;
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

        /// <param name="retryAmbiguous">
        /// Whether an outcome that MIGHT already have been applied (5xx, or a
        /// transport timeout) may be retried. True for the absolute snapshot
        /// feed, where REPLACE makes a re-send harmless. MUST be false for the
        /// additive movement feed: there a re-send adds the quantity a second
        /// time, and nothing downstream ever re-reads a balance to correct it.
        ///
        /// A 429 is retried either way — rate limiting is a refusal, so the
        /// request provably did not apply. That is the only failure we can
        /// distinguish; 5xx and timeouts are genuinely unknowable from here,
        /// which is why an idempotency key is on the open-questions list.
        /// </param>
        private async Task<BatchOutcome> PostWithRetry(HttpClient http, string url, string body,
                                                       IDictionary<string, string> headers,
                                                       bool retryAmbiguous = true)
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
                        if (!retryAmbiguous)
                        {
                            // May already have been applied. Under ADD a retry
                            // would add the quantity twice, so stop and let the
                            // run surface it instead.
                            result.Ambiguous = true;
                            break;
                        }
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
                    // Timeout / transport error. The request may have been
                    // received and applied before the connection dropped, so
                    // this is the same unknowable case as a 5xx: retried only
                    // for the REPLACE feed (spec 11.1), never for movements.
                    result.Code = 599;
                    result.Body = "Transport: " + ex.Message;
                    if (!retryAmbiguous) { result.Ambiguous = true; break; }
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
