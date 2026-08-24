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
    /// Townkart / Unthinkable Feed 2 — non-sale stock movement, PULL.
    ///
    /// Feed 2 is a PULL, not a push. UT call this with a store code on their
    /// own scheduler and apply the deltas inside their own database. We never
    /// POST for Feed 2. That single fact removes most of what the push design
    /// needed: there is no adjustmentType on the wire, no idempotency key, no
    /// ambiguous-retry problem, no per-store baseline ledger and no partial
    /// store, because there is no batch and no envelope. Dedupe is on their
    /// side, against the response key.
    ///
    /// Feed 1 (the absolute 5 AM snapshot, adjustmentType REPLACE) is
    /// unchanged and correct, and lives in TownkartPushController. This is a
    /// SEPARATE controller on purpose: the balance path must not be touched,
    /// and the push controller's movement endpoint is the wrong shape and is
    /// not being extended.
    ///
    /// Why movement and never balance: POS sales only post to SAP at 11 PM,
    /// so any SAP balance read during the day is stale by exactly that day's
    /// sales. UT's worked example — SAP 10 at 05:00, 2 sold POS and 2 sold
    /// web during the day, a GRT of 2 arrives, truth is 4. SAP says 8, UT
    /// hold 6. Sending 8 is as wrong as leaving 6. Only "-2" reconciles,
    /// because only UT know what their own sales already took off.
    ///
    /// FM: Z_INV_MOVE_UT_V2, FG ZINV_UT_MOV2, TR S4DK928278.
    ///     Nets SHKZG-signed MENGE per WERKS + MBLNR + MJAHR + MATNR over
    ///     storage locations 0001/0002, unrestricted stock only, windowed on
    ///     MKPF-CPUDT/CPUTM. Z_INV_MOVE_UT_V1 was built for the push model
    ///     and nets per material per window with no document grain and no
    ///     timestamp; it is superseded here and must not be used for Feed 2.
    ///
    /// Endpoint:
    ///   GET /api/ut/movements
    ///     ?store=HA10               (required, exactly one SAP site)
    ///     &amp;from=2026-08-22T16:05:00+05:30   (optional; default now-30min)
    ///     &amp;to=...                (optional; default SAP now)
    ///     &amp;lookbackMinutes=30    (used only when from is omitted)
    ///     &amp;env=dev|qa|prod
    ///
    /// Auth (theirs): X-Api-Token, matched against the UT_PULL_TOKEN setting.
    ///   This is a credential UT present to US and is NOT the outbound
    ///   X-Api-Token we send to Townkart for Feed 1 — different direction,
    ///   different secret, different expiry. It is read from appSettings or a
    ///   machine environment variable and is never committed: this repo is
    ///   public. If it is unset the endpoint refuses every call rather than
    ///   defaulting open.
    /// </summary>
    [RoutePrefix("api/ut")]
    public class TownkartMovementPullController : BaseController
    {
        private const string MOVE_FM = "Z_INV_MOVE_UT_V2";

        // Kept as the documented default rather than a magic number. UT are
        // instructed to call every 15 minutes against this 30-minute window
        // and dedupe on all four key fields. The overlap is deliberate: it
        // makes gaps structurally impossible regardless of scheduler jitter,
        // but it only works if they actually dedupe.
        private const int DEFAULT_LOOKBACK_MIN = 30;

        // The four fields that identify a movement line. MJAHR is in here
        // because MBLNR restarts each fiscal year and both years sit in the
        // same pool — 4942777509/2026 alongside 4933278897/2025. Dedupe
        // without it and UT silently discard legitimate movements, but only
        // on colliding numbers, which is nearly undiagnosable from their end.
        private static readonly string[] DEDUPE_KEY =
        {
            "storeCode", "docNo", "docYear", "itemSKU"
        };

        [HttpGet]
        [Route("movements")]
        public IHttpActionResult Movements(
            string store = null,
            string from = null,
            string to = null,
            int lookbackMinutes = DEFAULT_LOOKBACK_MIN,
            string env = "dev",
            int maxRows = 50000,
            string seg = null,
            string exclBwart = null,
            string pool = null,
            string unrestrictedOnly = null)
        {
            var runStart = DateTime.Now;

            string expected = ReadSetting("UT_PULL_TOKEN");
            if (string.IsNullOrWhiteSpace(expected))
            {
                // Fail closed. An unconfigured host must not serve stock
                // movement to an unauthenticated caller.
                return Content(HttpStatusCode.ServiceUnavailable, new
                {
                    status = false,
                    error = "not_configured",
                    message = "UT_PULL_TOKEN is not set on this host, so this endpoint " +
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

            // One store per call, and it must look like a site code. A CSV
            // would be CONDENSEd into a single token by the FM, match no
            // plant, and come back as an empty array — which is
            // indistinguishable from "nothing moved" and would have UT
            // quietly believing a store is idle.
            string site = (store ?? "").Trim().ToUpperInvariant();
            if (site.Length == 0)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "store_required",
                    message = "store is required — exactly one SAP site code, e.g. store=HA10."
                });
            }
            if (site.Length > 4 || !site.All(char.IsLetterOrDigit))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "store_invalid",
                    message = "store must be a single SAP site code of up to 4 alphanumeric " +
                              "characters. Lists are not supported — call once per store."
                });
            }

            if (lookbackMinutes < 1 || lookbackMinutes > 1440)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "lookback_invalid",
                    message = "lookbackMinutes must be 1..1440."
                });
            }

            DateTime fromTs = DateTime.MinValue, toTs = DateTime.MinValue;
            if (!string.IsNullOrWhiteSpace(from) && !TryParseStamp(from, out fromTs))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "from_invalid",
                    message = "from must be ISO 8601 (2026-08-22T16:05:00+05:30), " +
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
            if (fromTs != DateTime.MinValue && toTs != DateTime.MinValue && fromTs >= toTs)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "window_invalid",
                    message = "from must be earlier than to."
                });
            }

            RfcConfigParameters rfcPar;
            if (!TryResolveEnv(env, out rfcPar))
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    status = false,
                    error = "env_invalid",
                    message = "env must be dev, qa or prod."
                });
            }

            RfcDestination dest;
            try
            {
                dest = RfcDestinationManager.GetDestination(rfcPar);
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

            JArray movements;
            string windowFrom, windowTo, fmMessage;
            int rows, groups, emitted, netZero, pos, neg;
            bool truncated;

            try
            {
                IRfcFunction fn = dest.Repository.CreateFunction(MOVE_FM);
                fn.SetValue("IV_WERKS", site);
                fn.SetValue("IV_LOOKBACK_MIN", lookbackMinutes);
                fn.SetValue("IV_MAX_ROWS", maxRows);

                if (fromTs != DateTime.MinValue)
                {
                    fn.SetValue("IV_FROM_DT", fromTs.ToString("yyyyMMdd"));
                    fn.SetValue("IV_FROM_TM", fromTs.ToString("HHmmss"));
                }
                if (toTs != DateTime.MinValue)
                {
                    fn.SetValue("IV_TO_DT", toTs.ToString("yyyyMMdd"));
                    fn.SetValue("IV_TO_TM", toTs.ToString("HHmmss"));
                }

                // Left unset unless overridden, so the FM applies its own
                // documented defaults: pool 0001/0002, segments APP/GM/FBG,
                // unrestricted stock only, and movement types 251/252/601/602
                // excluded. Those defaults live in one place — the FM — and
                // are not restated here, so they cannot drift apart.
                if (!string.IsNullOrWhiteSpace(seg)) fn.SetValue("IV_SEG_CSV", seg);
                if (!string.IsNullOrWhiteSpace(exclBwart)) fn.SetValue("IV_EXCL_BWART", exclBwart);
                if (!string.IsNullOrWhiteSpace(pool)) fn.SetValue("IV_POOL_CSV", pool);
                if (!string.IsNullOrWhiteSpace(unrestrictedOnly))
                    fn.SetValue("IV_UNRES_ONLY", unrestrictedOnly);

                fn.Invoke(dest);

                rows = fn.GetInt("EV_ROWS");
                groups = fn.GetInt("EV_GROUPS");
                emitted = fn.GetInt("EV_EMITTED");
                netZero = fn.GetInt("EV_NETZERO");
                pos = fn.GetInt("EV_POS_COUNT");
                neg = fn.GetInt("EV_NEG_COUNT");
                truncated = (fn.GetString("EV_TRUNC") ?? "").Trim() == "X";
                fmMessage = fn.GetString("EV_MESSAGE");
                windowFrom = fn.GetString("EV_FROM_TS");
                windowTo = fn.GetString("EV_TO_TS");

                string json = fn.GetString("EV_JSON");
                movements = string.IsNullOrWhiteSpace(json) || json.Length <= 2
                    ? new JArray()
                    : JArray.Parse(json);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadGateway, new
                {
                    status = false,
                    error = "sap_call_failed",
                    message = MOVE_FM + " failed: " + ex.Message
                });
            }

            // A truncated window is NOT an empty result, and must never be
            // returned as a 200 with an empty array. The FM deliberately
            // returns [] rather than a partial window, because a partial one
            // silently loses movements; if that reached UT as success they
            // would move past a window they never actually received, and
            // nothing downstream re-reads a balance to correct it.
            if (truncated)
            {
                return Content(HttpStatusCode.Conflict, new
                {
                    status = false,
                    error = "window_too_dense",
                    message = "More than " + maxRows + " movement rows in this window. " +
                              "Nothing is returned rather than a partial window. Narrow " +
                              "the window with from/to and call again.",
                    store = site,
                    windowFrom,
                    windowTo,
                    rowsSeen = rows
                });
            }

            // The movement array is passed through EXACTLY as SAP built it.
            // Nothing here reshapes or filters it, and in particular nothing
            // clamps a negative to zero. Feed 1's MapItems does clamp,
            // because a negative BALANCE is nonsense — but here the negative
            // IS the message, and clamping would delete every outbound
            // movement while still reporting success.
            return Content(HttpStatusCode.OK, new
            {
                status = true,
                store = site,
                windowFrom,
                windowTo,
                count = movements.Count,
                movements,
                dedupeOn = DEDUPE_KEY,
                notes = new
                {
                    dedupe = "Call every " + (lookbackMinutes / 2) + " minutes against this " +
                             lookbackMinutes + "-minute window and dedupe on all four fields " +
                             "in dedupeOn. The overlap is deliberate — it makes gaps " +
                             "structurally impossible regardless of scheduler jitter, but " +
                             "only if you dedupe.",
                    ledger = "Never clear the applied-document ledger when the daily Feed 1 " +
                             "file loads. Resetting it makes the next pull re-deliver " +
                             "pre-cutoff documents, which then get applied on top of an " +
                             "absolute figure that already contained them — in the same " +
                             "direction, every morning.",
                    sign = "movement is signed and relative. Negative means stock left the " +
                           "store, positive means it arrived. Apply it to the running total; " +
                           "do not treat it as a balance and do not clamp it at zero.",
                    excluded = "Movements Axapta already reports to you are excluded at " +
                               "source (POS sale and its return), as is the nightly e-commerce " +
                               "goods issue. Everything else is carried by default."
                },
                diagnostics = new
                {
                    fm = MOVE_FM,
                    env,
                    msegRows = rows,
                    documentGroups = groups,
                    nettedToZero = netZero,
                    emitted,
                    positive = pos,
                    negative = neg,
                    fmMessage,
                    durationMs = (int)(DateTime.Now - runStart).TotalMilliseconds
                }
            });
        }

        /// <summary>
        /// Accepts ISO 8601 with or without an offset, plus the two compact
        /// forms. An offset is honoured and converted to server local time,
        /// because the FM windows on MKPF-CPUDT/CPUTM, which are recorded in
        /// the application server's own time. Feed 1 left IST-versus-UTC
        /// unresolved with the vendor (their O8); this endpoint does not
        /// inherit that ambiguity — it states the offset on the way out and
        /// honours one on the way in.
        /// </summary>
        private static bool TryParseStamp(string raw, out DateTime value)
        {
            value = DateTime.MinValue;
            string s = (raw ?? "").Trim();
            if (s.Length == 0) return false;

            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dto))
            {
                value = dto.ToLocalTime().DateTime;
                return true;
            }

            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyyMMddHHmmss",
                "yyyy-MM-dd"
            };
            return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture,
                                          DateTimeStyles.None, out value);
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
