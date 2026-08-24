using Newtonsoft.Json.Linq;
using SAP.Middleware.Connector;
using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inventory
{
    /// <summary>
    /// Green Honchos (Green On) store stock — PULL.
    ///
    /// Green Honchos have until now been fed by a PUSH: a scheduled job runs
    /// Z_GO_INV_FULL_V4 or Z_GO_INV_DELTA_V3, which build the payload and POST
    /// it through the .36 relay to engine.kartmax.in. That path stays exactly
    /// as it is and is not touched here. This controller adds the pull side so
    /// the vendor can read on their own schedule instead of waiting for ours.
    ///
    /// The two models differ in one way that matters more than any other: WHO
    /// HOLDS THE CURSOR.
    ///
    ///   push  — SAP holds it, in ZGO_WATERMARK, and advances it only when
    ///           every batch of a run posted successfully.
    ///   pull  — the CALLER holds it. Neither FM behind this controller reads
    ///           or writes ZGO_WATERMARK. A pull the caller failed to persist
    ///           has to be repeatable, and a server-side cursor would have
    ///           already moved past it.
    ///
    /// That is why the delta endpoint returns nextCursor and why the caller
    /// must store it. It is also why running the pull alongside the scheduled
    /// push is safe: they cannot move each other's position.
    ///
    /// Two endpoints, two different jobs, and the difference is deliberate:
    ///
    ///   GET /api/gh/inventory        absolute snapshot, POSITIVE STOCK ONLY.
    ///                                Absence of a pair means zero. 99.9% of
    ///                                MARD rows are zero — one measured page
    ///                                of 25 stores was 184 rows with the floor
    ///                                at 1 and 157,959 with it at 0 — so
    ///                                serving zeros here would be almost all
    ///                                payload and no information.
    ///
    ///   GET /api/gh/inventory/delta  what changed in a window, and this one
    ///                                DOES emit zeros. A pair that changed to
    ///                                zero is precisely the signal that stops
    ///                                the vendor overselling it. Dropping
    ///                                zeros is the live V1 defect the delta
    ///                                feed exists to correct, so there is no
    ///                                minimum-quantity parameter on it at all.
    ///
    /// Quantities on BOTH endpoints are absolute current stock for the pair,
    /// never a movement quantity. This is the opposite of the Townkart feed in
    /// TownkartMovementPullController, where rows are signed and additive and
    /// must be applied to a running total. Here the newest row for a pair
    /// simply wins, replaying a window is idempotent, and page order does not
    /// matter. Do not add these to anything.
    ///
    /// FMs:
    ///   Z_GO_INV_PULL_V2   FG ZGO_PULL2,  TR S4DK928326
    ///   Z_GO_DELTA_PULL_V1 FG ZGO_DPULL1, TR S4DK928326
    ///
    /// Store scope: T001W-VLFKZ is 'A' for a store in this system and 'B' for
    /// a DC / hub / factory — the convention reads inverted from the usual
    /// one, so it is asserted rather than assumed, and both FMs abort if no
    /// VLFKZ 'A' plant exists rather than returning a silent zero. DH24
    /// RDC-FRK and 41 other DCs are 'B'. The filter is applied inside SQL
    /// whether or not the caller names plants, so asking for a DC by name
    /// returns nothing rather than DC stock.
    ///
    /// Auth (theirs): X-Api-Token, matched against the GH_PULL_TOKEN setting.
    ///   Separate secret from UT_PULL_TOKEN and from the outbound Green On
    ///   credential in ZAPI_AI_CREDS — three different directions, three
    ///   different secrets. Read from appSettings or a machine environment
    ///   variable and never committed: this repo is public. Unset means the
    ///   endpoint refuses every call rather than defaulting open.
    /// </summary>
    [RoutePrefix("api/gh")]
    public class GreenHonchosInventoryPullController : BaseController
    {
        private const string SNAP_FM = "Z_GO_INV_PULL_V2";
        private const string DELTA_FM = "Z_GO_DELTA_PULL_V1";

        // One store per page by default. A PROD store carries roughly 2,500
        // positive pairs, so the FM's own ceiling of 25 stores would be a
        // ~3.6 MB response — allowed, but never the default a caller falls
        // into by omitting the field.
        private const int DEFAULT_PAGE_STORES = 1;
        private const int MAX_PAGE_STORES = 25;

        // ---------------------------------------------------------------
        // Snapshot
        // ---------------------------------------------------------------

        [HttpGet]
        [Route("inventory")]
        public IHttpActionResult Inventory(
            string store = null,
            int offset = 0,
            int limit = DEFAULT_PAGE_STORES,
            int minQty = 1,
            int maxRows = 50000,
            string seg = null,
            string env = "dev")
        {
            var runStart = DateTime.Now;

            IHttpActionResult authFail = CheckToken();
            if (authFail != null) return authFail;

            string site = (store ?? "").Trim().ToUpperInvariant();
            if (site.Length > 0 && (site.Length > 4 || !site.All(char.IsLetterOrDigit)))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "store_invalid",
                    message = "store must be a single SAP site code of up to 4 alphanumeric " +
                              "characters. Omit it to walk every store with offset/limit."
                });
            }

            if (offset < 0)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "offset_invalid",
                    message = "offset must be 0 or greater."
                });
            }
            if (limit < 1 || limit > MAX_PAGE_STORES)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "limit_invalid",
                    message = "limit is a number of STORES per page, 1.." + MAX_PAGE_STORES +
                              ". It is not a row count — row counts vary with assortment, " +
                              "store counts do not, which is what makes the walk resumable."
                });
            }

            RfcConfigParameters rfcPar;
            if (!TryResolveEnv(env, out rfcPar))
                return EnvInvalid();

            RfcDestination dest;
            IHttpActionResult connFail = TryConnect(rfcPar, out dest);
            if (connFail != null) return connFail;

            JArray inventory;
            string fmMessage;
            int count, rowsSeen, storesTotal, storesReturned, nextOffset, negCount, guard;
            bool truncated, hasMore;

            try
            {
                IRfcFunction fn = dest.Repository.CreateFunction(SNAP_FM);

                // A named store and a page walk are the same mechanism: the
                // store filter narrows the plant list, offset/limit slice it.
                // Naming one store makes the slice a single row, so limit is
                // forced to 1 rather than silently ignored.
                if (site.Length > 0)
                {
                    fn.SetValue("IV_WERKS_CSV", site);
                    fn.SetValue("IV_OFFSET", 0);
                    fn.SetValue("IV_LIMIT", 1);
                }
                else
                {
                    fn.SetValue("IV_OFFSET", offset);
                    fn.SetValue("IV_LIMIT", limit);
                }

                fn.SetValue("IV_MIN_QTY", minQty);
                fn.SetValue("IV_MAX_ROWS", maxRows);
                if (!string.IsNullOrWhiteSpace(seg)) fn.SetValue("IV_SEG_CSV", seg);

                fn.Invoke(dest);

                count = fn.GetInt("EV_COUNT");
                rowsSeen = fn.GetInt("EV_ROWS_SEEN");
                storesTotal = fn.GetInt("EV_STORES_TOTAL");
                storesReturned = fn.GetInt("EV_STORES_RETURNED");
                nextOffset = fn.GetInt("EV_NEXT_OFFSET");
                negCount = fn.GetInt("EV_NEG_COUNT");
                guard = fn.GetInt("EV_STORE_COUNT");
                truncated = (fn.GetString("EV_TRUNCATED") ?? "").Trim() == "X";
                hasMore = (fn.GetString("EV_HAS_MORE") ?? "").Trim() == "X";
                fmMessage = fn.GetString("EV_MESSAGE");

                string json = fn.GetString("EV_JSON");
                inventory = string.IsNullOrWhiteSpace(json) || json.Length <= 2
                    ? new JArray()
                    : JArray.Parse(json);
            }
            catch (Exception ex)
            {
                return SapCallFailed(SNAP_FM, ex);
            }

            if (truncated)
            {
                return Content(HttpStatusCode.Conflict, new
                {
                    status = false,
                    error = "page_too_dense",
                    message = "More than " + maxRows + " stocked pairs on this page. Nothing " +
                              "is returned rather than a partial page, because a short page " +
                              "is indistinguishable from a store holding less stock. Lower " +
                              "limit and call again.",
                    rowsSeen,
                    offset,
                    limit
                });
            }

            return Content(HttpStatusCode.OK, new
            {
                status = true,
                count,
                inventory,
                page = new
                {
                    offset = site.Length > 0 ? 0 : offset,
                    limit = site.Length > 0 ? 1 : limit,
                    storesReturned,
                    storesTotal,
                    nextOffset,
                    hasMore = site.Length > 0 ? false : hasMore
                },
                semantics = new
                {
                    quantity = "inventory is ABSOLUTE current stock for the sku/store pair. " +
                               "It is not a movement and must not be added to a running " +
                               "total. The newest value for a pair simply replaces the " +
                               "previous one.",
                    zeros = "This endpoint returns POSITIVE stock only. A pair that is " +
                            "absent holds zero. Do not read absence as 'unchanged' — if you " +
                            "need to know the moment a pair reaches zero, poll " +
                            "/api/gh/inventory/delta, which does emit zero rows.",
                    paging = "limit is a number of STORES, not rows. Walk with nextOffset " +
                             "until hasMore is false. The plant list is sorted by site code, " +
                             "so a page is stable and the walk can be resumed after a " +
                             "failure without any cursor held on our side.",
                    scope = "Stores only. Distribution centres, hubs and factories are " +
                            "excluded in SQL and cannot be reached by naming one."
                },
                diagnostics = new
                {
                    fm = SNAP_FM,
                    env,
                    rowsSeen,
                    negativesClamped = negCount,
                    storePlantsInSystem = guard,
                    minQty,
                    fmMessage,
                    durationMs = (int)(DateTime.Now - runStart).TotalMilliseconds
                }
            });
        }

        // ---------------------------------------------------------------
        // Delta
        // ---------------------------------------------------------------

        [HttpGet]
        [Route("inventory/delta")]
        public IHttpActionResult InventoryDelta(
            string from = null,
            string to = null,
            string store = null,
            int maxPairs = 10000,
            string seg = null,
            string env = "dev")
        {
            var runStart = DateTime.Now;

            IHttpActionResult authFail = CheckToken();
            if (authFail != null) return authFail;

            // Required, and refused rather than defaulted. A missing cursor
            // would mean scanning MKPF from the beginning of time, and a
            // convenient default here would quietly become the thing every
            // caller relies on.
            if (string.IsNullOrWhiteSpace(from))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "from_required",
                    message = "from is required — it is your own cursor from the previous " +
                              "call's nextCursor. On the very first call use a recent " +
                              "timestamp, not an open-ended one."
                });
            }

            DateTime fromTs, toTs = DateTime.MinValue;
            if (!TryParseStamp(from, out fromTs))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "from_invalid",
                    message = "from must be ISO 8601 (2026-08-24T16:05:00+05:30), " +
                              "'yyyy-MM-dd HH:mm:ss' or 'yyyyMMddHHmmss'."
                });
            }
            if (!string.IsNullOrWhiteSpace(to) && !TryParseStamp(to, out toTs))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "to_invalid",
                    message = "to must be ISO 8601, 'yyyy-MM-dd HH:mm:ss' or 'yyyyMMddHHmmss'."
                });
            }
            if (toTs != DateTime.MinValue && fromTs >= toTs)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "window_invalid",
                    message = "from must be earlier than to."
                });
            }

            string site = (store ?? "").Trim().ToUpperInvariant();
            if (site.Length > 0 && (site.Length > 4 || !site.All(char.IsLetterOrDigit)))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "store_invalid",
                    message = "store must be a single SAP site code of up to 4 alphanumeric " +
                              "characters. Omit it for every store."
                });
            }

            RfcConfigParameters rfcPar;
            if (!TryResolveEnv(env, out rfcPar))
                return EnvInvalid();

            RfcDestination dest;
            IHttpActionResult connFail = TryConnect(rfcPar, out dest);
            if (connFail != null) return connFail;

            JArray inventory;
            string fmMessage, wFromDt, wFromTm, wToDt, wToTm;
            int count, pairs, zeroCount, negCount, guard;
            bool truncated;

            try
            {
                IRfcFunction fn = dest.Repository.CreateFunction(DELTA_FM);
                fn.SetValue("IV_FROM_DT", fromTs.ToString("yyyyMMdd"));
                fn.SetValue("IV_FROM_TM", fromTs.ToString("HHmmss"));
                if (toTs != DateTime.MinValue)
                {
                    fn.SetValue("IV_TO_DT", toTs.ToString("yyyyMMdd"));
                    fn.SetValue("IV_TO_TM", toTs.ToString("HHmmss"));
                }
                if (site.Length > 0) fn.SetValue("IV_WERKS_CSV", site);
                if (!string.IsNullOrWhiteSpace(seg)) fn.SetValue("IV_SEG_CSV", seg);
                fn.SetValue("IV_MAX_PAIRS", maxPairs);

                fn.Invoke(dest);

                count = fn.GetInt("EV_COUNT");
                pairs = fn.GetInt("EV_PAIRS");
                zeroCount = fn.GetInt("EV_ZERO_COUNT");
                negCount = fn.GetInt("EV_NEG_COUNT");
                guard = fn.GetInt("EV_STORE_COUNT");
                truncated = (fn.GetString("EV_TRUNCATED") ?? "").Trim() == "X";
                fmMessage = fn.GetString("EV_MESSAGE");
                wFromDt = fn.GetString("EV_FROM_DT");
                wFromTm = fn.GetString("EV_FROM_TM");
                wToDt = fn.GetString("EV_TO_DT");
                wToTm = fn.GetString("EV_TO_TM");

                string json = fn.GetString("EV_JSON");
                inventory = string.IsNullOrWhiteSpace(json) || json.Length <= 2
                    ? new JArray()
                    : JArray.Parse(json);
            }
            catch (Exception ex)
            {
                return SapCallFailed(DELTA_FM, ex);
            }

            // A truncated window is not an empty result and must never be a
            // 200 with an empty array. The FM returns nothing rather than a
            // partial window; if a partial one arrived as success the caller
            // would advance past pairs they never received, and — unlike the
            // Townkart movement feed — no later absolute read would put them
            // back, because a pair whose stock has since stopped changing is
            // never revisited by any window.
            if (truncated)
            {
                return Content(HttpStatusCode.Conflict, new
                {
                    status = false,
                    error = "window_too_dense",
                    message = "More than " + maxPairs + " changed pairs in this window. " +
                              "Nothing is returned rather than a partial window. Keep your " +
                              "existing cursor, narrow the window with to, and call again.",
                    pairsSeen = pairs,
                    windowFrom = Stamp(wFromDt, wFromTm),
                    windowTo = Stamp(wToDt, wToTm)
                });
            }

            // Never hand back a cursor that would move the caller backwards.
            // The FM bounds the window at SAP's clock, so a caller polling
            // faster than that clock advances can be handed a 'to' that is at
            // or behind the 'from' they sent. Returning their own cursor is
            // correct there: nothing was skipped, nothing was read.
            DateTime toResolved;
            string nextCursor;
            if (TryParseStamp(wToDt + wToTm, out toResolved) && toResolved > fromTs)
                nextCursor = toResolved.ToString("yyyy-MM-ddTHH:mm:ss");
            else
                nextCursor = fromTs.ToString("yyyy-MM-ddTHH:mm:ss");

            return Content(HttpStatusCode.OK, new
            {
                status = true,
                count,
                inventory,
                window = new
                {
                    from = Stamp(wFromDt, wFromTm),
                    to = Stamp(wToDt, wToTm)
                },
                nextCursor,
                semantics = new
                {
                    cursor = "Persist nextCursor and send it as from on the next call. We " +
                             "hold no cursor for you — that is deliberate, so a call you " +
                             "failed to store can simply be repeated. Advance it only after " +
                             "the rows are committed on your side, and never advance it on a " +
                             "409 or an error.",
                    window = "from is exclusive, to is inclusive, and to is bounded at read " +
                             "time rather than left open. That is what makes a replay of the " +
                             "same from/to return byte-identical rows, and what stops a " +
                             "document posted mid-call from landing in both windows or in " +
                             "neither.",
                    quantity = "inventory is ABSOLUTE current stock for the pair, not a " +
                               "movement. Replace, never add. Replaying a window is therefore " +
                               "harmless and page order does not matter.",
                    zeros = "Zero rows ARE included here, and they are the point of this " +
                            "endpoint. A pair reported at 0 has sold out or been moved out; " +
                            "take it off sale. Suppressing zeros is what causes overselling.",
                    scope = "Stores only, and only pairs whose stock actually moved in the " +
                            "window. A pair absent from a delta has not changed — unlike the " +
                            "snapshot, absence here does NOT mean zero."
                },
                diagnostics = new
                {
                    fm = DELTA_FM,
                    env,
                    changedPairs = pairs,
                    zeroRows = zeroCount,
                    negativesClamped = negCount,
                    storePlantsInSystem = guard,
                    fmMessage,
                    durationMs = (int)(DateTime.Now - runStart).TotalMilliseconds
                }
            });
        }

        // ---------------------------------------------------------------
        // Shared
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns null when the caller is authenticated, or the response to
        /// send back when they are not. Fails closed on an unconfigured host:
        /// stock levels must not be served to an unauthenticated caller
        /// because a secret happened to be missing.
        /// </summary>
        private IHttpActionResult CheckToken()
        {
            string expected = ReadSetting("GH_PULL_TOKEN");
            if (string.IsNullOrWhiteSpace(expected))
            {
                return Content(HttpStatusCode.ServiceUnavailable, new
                {
                    status = false,
                    error = "not_configured",
                    message = "GH_PULL_TOKEN is not set on this host, so this endpoint " +
                              "cannot authenticate callers and refuses to serve. Set it as " +
                              "a machine environment variable or in secrets.config. Never " +
                              "commit it — this repo is public."
                });
            }

            System.Collections.Generic.IEnumerable<string> tokenHeaders;
            string presented = Request.Headers.TryGetValues("X-Api-Token", out tokenHeaders)
                ? (tokenHeaders.FirstOrDefault() ?? "")
                : "";

            if (!FixedTimeEquals(presented, expected))
            {
                return Content(HttpStatusCode.Unauthorized, new
                {
                    status = false,
                    error = "unauthorized",
                    message = "Missing or invalid X-Api-Token."
                });
            }
            return null;
        }

        private IHttpActionResult EnvInvalid()
        {
            return Content(HttpStatusCode.BadRequest, new
            {
                status = false,
                error = "env_invalid",
                message = "env must be dev, qa or prod."
            });
        }

        private IHttpActionResult TryConnect(RfcConfigParameters rfcPar, out RfcDestination dest)
        {
            dest = null;
            try
            {
                dest = RfcDestinationManager.GetDestination(rfcPar);
                return null;
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadGateway, new
                {
                    status = false,
                    error = "sap_unavailable",
                    message = "RFC connect failed: " + ex.Message
                });
            }
        }

        private IHttpActionResult SapCallFailed(string fm, Exception ex)
        {
            return Content(HttpStatusCode.BadGateway, new
            {
                status = false,
                error = "sap_call_failed",
                message = fm + " failed: " + ex.Message
            });
        }

        /// <summary>
        /// SAP returns the window as separate DATS and TIMS strings. Joined
        /// into one ISO-like stamp so the caller never has to reassemble two
        /// fields, and so the value can be handed straight back as from.
        /// </summary>
        private static string Stamp(string dt, string tm)
        {
            string d = (dt ?? "").Trim();
            string t = (tm ?? "").Trim().PadRight(6, '0');
            if (d.Length != 8) return "";
            return d.Substring(0, 4) + "-" + d.Substring(4, 2) + "-" + d.Substring(6, 2) +
                   "T" + t.Substring(0, 2) + ":" + t.Substring(2, 2) + ":" + t.Substring(4, 2);
        }

        /// <summary>
        /// Accepts ISO 8601 with or without an offset, plus the two compact
        /// forms. An offset is honoured and converted to server local time,
        /// because the FM windows on MKPF-CPUDT/CPUTM, which are recorded in
        /// the application server's own time.
        /// </summary>
        private static bool TryParseStamp(string raw, out DateTime value)
        {
            value = DateTime.MinValue;
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return false;

            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyyMMddHHmmss",
                "yyyyMMdd",
                "yyyy-MM-dd"
            };
            // Exact forms are tried first. DateTimeOffset.TryParse would
            // happily read "20260824171932" as something else entirely on
            // some cultures, and the compact form is what SAP hands back.
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out value))
                return true;

            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out dto))
            {
                value = dto.ToLocalTime().DateTime;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Length-independent comparison, so a wrong token cannot be
        /// recovered a character at a time from response timing.
        /// </summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        /// <summary>
        /// appSettings first (which picks up secrets.config), then a machine
        /// environment variable. deploy-iis.yml force-copies the repo
        /// Web.config over the box on every deploy, so a secret must never
        /// live in Web.config itself.
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
