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
using System.Web.Hosting;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Generic
{
    /// <summary>
    /// Asynchronous front door for slow RFCs.
    ///
    /// WHY THIS EXISTS
    /// sap-api.v2retail.net is Cloudflare-proxied and Cloudflare gives up on the
    /// origin at ~125 seconds. That is the ONLY wall in the chain: IIS allows 600s
    /// (httpRuntime executionTimeout in Web.config). One variant creation through
    /// ZMM_VAR_ART_CRT_V6 costs 55-120s in PROD (p50 ~90s, measured over 30
    /// successes on 26-27 Aug 2026), so it sits inside the budget by seconds and
    /// crosses it on any slow day.
    ///
    /// SAP does not stop when the edge gives up. The FM runs to completion and
    /// commits, so a 524 is a LOST ANSWER, not a failed write - and callers that
    /// read it as a failure retry and duplicate undeletable master data. Four real,
    /// priced PROD variants were recorded as never created that way on 28/30/31-Aug.
    ///
    /// WHAT THIS DOES
    /// POST /api/rfc/jobs?env=prod   - identical body to /api/rfc/proxy. Returns 202
    ///                                 immediately with a job id. Never blocks.
    /// GET  /api/rfc/jobs/{id}       - the stored outcome: running | done | failed.
    ///
    /// The caller therefore gets its answer over two short requests instead of one
    /// long one, and no answer is ever lost to the edge timeout.
    ///
    /// HOW IT RUNS THE RFC
    /// The background worker POSTs to the EXISTING /api/rfc/proxy over LOOPBACK.
    /// That is deliberate: loopback does not pass through Cloudflare, so the 125s
    /// wall does not apply, and every rule the sync proxy has earned the hard way -
    /// the strict parameter gate, the NCo stale-metadata self-heal, the Java-MW
    /// label row, the EX_RETURN synthesis - is reused rather than reimplemented.
    /// Re-deriving that logic here would create a second copy that silently drifts,
    /// which is the failure mode already documented for the hand-published
    /// Routemaster sites on ports 9010/9011. The extra in-process hop costs
    /// milliseconds against a 90-second RFC.
    ///
    /// Nothing in GenericRfcProxyController is modified by this file. If this
    /// controller is removed the sync proxy is entirely unaffected.
    ///
    /// Security: same X-RFC-Key header as the sync proxy.
    /// </summary>
    public class RfcJobController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";

        /// <summary>Where the worker reaches this same site. MUST be loopback: going
        /// back out through the public host would re-enter Cloudflare and reinstate
        /// the very timeout this endpoint exists to escape. Overridable in Web.config
        /// (appSetting RfcJobs.SelfBaseUrl) so a port change needs no redeploy.</summary>
        private static string SelfBaseUrl()
        {
            string cfg = ConfigurationManager.AppSettings["RfcJobs.SelfBaseUrl"];
            return string.IsNullOrWhiteSpace(cfg) ? "http://localhost:9292" : cfg.TrimEnd('/');
        }

        /// <summary>Long enough for the slowest article creation seen (118.7s) with
        /// generous headroom, and still well inside IIS's 600s.</summary>
        private static TimeSpan WorkerTimeout()
        {
            string cfg = ConfigurationManager.AppSettings["RfcJobs.TimeoutSeconds"];
            int secs;
            if (!int.TryParse(cfg, out secs) || secs <= 0) secs = 480;
            return TimeSpan.FromSeconds(secs);
        }

        /// <summary>
        /// One client for the process. A per-request HttpClient leaves its socket
        /// in TIME_WAIT for ~4 minutes, which is fine for a handful of variants and
        /// is socket exhaustion the moment anyone loops this. Timeout is disabled
        /// here and applied per call through a linked CancellationTokenSource.
        /// </summary>
        private static readonly HttpClient Http =
            new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>
        /// HTTP 200 does NOT mean the RFC worked. GenericRfcProxyController answers
        /// 200 with EX_RETURN.TYPE = "E" for ABAP, communication, logon and
        /// unauthorized failures alike - only the strict-parameter gate returns a
        /// non-2xx. Keying job status off IsSuccessStatusCode alone would file every
        /// one of those as "done", which is exactly the kind of false success this
        /// endpoint exists to stamp out. Returns "" when no verdict can be read.
        /// </summary>
        private static void ReadSapVerdict(JToken parsed, bool skipLabelRow,
                                           out string type, out string message)
        {
            type = "";
            message = "";
            JObject root = parsed as JObject;
            if (root == null) return;

            JToken exr = root["EX_RETURN"];

            JObject single = exr as JObject;
            if (single != null)
            {
                type = RowString(single, "TYPE").Trim().ToUpperInvariant();
                message = RowString(single, "MESSAGE").Trim();
                return;
            }

            JArray rows = exr as JArray;
            if (rows == null) return;

            // labelrow prepends a DDIC label row to EVERY table, so row 0 of a
            // TABLES-parameter EX_RETURN reads TYPE = "Message Type". Treating that
            // as the verdict passes every SAP error off as a success - the precise
            // failure this method exists to prevent, in the mode PROD is moving to.
            int start = skipLabelRow ? 1 : 0;

            string firstType = "";
            string firstMessage = "";
            bool haveFirst = false;

            for (int i = start; i < rows.Count; i++)
            {
                JObject row = rows[i] as JObject;
                if (row == null) continue;

                string t = RowString(row, "TYPE").Trim().ToUpperInvariant();
                string m = RowString(row, "MESSAGE").Trim();
                if (!haveFirst) { firstType = t; firstMessage = m; haveFirst = true; }

                // A BAPIRET2-style table routinely carries S/W rows before the one
                // that matters, so the verdict is the first FAILING row, not row 0.
                if (IsFailureType(t)) { type = t; message = m; return; }
            }

            type = firstType;
            message = firstMessage;
        }

        /// <summary>
        /// SAP severities that mean the call did not succeed. A is an abort and X a
        /// short dump; filing either as "done" because it is not literally "E" would
        /// report the two most severe outcomes SAP has as successes.
        /// </summary>
        private static bool IsFailureType(string type)
        {
            return type == "E" || type == "A" || type == "X";
        }

        /// <summary>
        /// Read one cell as a string without trusting its JSON type. Newtonsoft's
        /// explicit JToken-to-string cast throws on a JObject or JArray, and this
        /// runs against whatever SAP returned.
        /// </summary>
        private static string RowString(JObject row, string key)
        {
            JValue cell = row[key] as JValue;
            if (cell == null || cell.Value == null) return "";
            return Convert.ToString(cell.Value,
                       System.Globalization.CultureInfo.InvariantCulture) ?? "";
        }

        /// <summary>
        /// Will the proxy prepend label rows to this call's tables? Mirrors
        /// GenericRfcProxyController.WantsLabelRow, including the global
        /// RfcProxy.LabelRow appSetting - which turns label rows on for every call
        /// with no query parameter at all, so reading only the forwarded one would
        /// miss it.
        /// </summary>
        private static bool LabelRowExpected(string labelRow)
        {
            if (!string.IsNullOrEmpty(labelRow))
            {
                if (labelRow == "1" || labelRow.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (labelRow == "0" || labelRow.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }

            string cfg = ConfigurationManager.AppSettings["RfcProxy.LabelRow"];
            return !string.IsNullOrEmpty(cfg) &&
                   (cfg == "1" || cfg.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        private static string JobDir()
        {
            string dir = HostingEnvironment.MapPath("~/App_Data/rfcjobs");
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static string JobPath(string id)
        {
            return Path.Combine(JobDir(), id + ".json");
        }

        /// <summary>
        /// Job state lives on disk, not in memory. V2RfcTestPool is recycled by a
        /// watchdog whenever NCo wedges; an in-memory dictionary would lose every
        /// in-flight job on that recycle and the caller would poll a job id that no
        /// longer exists. On disk the record survives, and a job still marked
        /// "running" after a recycle is an honest answer rather than a 404.
        /// </summary>
        private static void Write(string id, JObject rec)
        {
            string path = JobPath(id);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, rec.ToString(Newtonsoft.Json.Formatting.None), new UTF8Encoding(false));
            // File.Replace swaps in one step. Delete-then-Move leaves a window in
            // which the file does not exist, and a poller landing in it would be
            // told a job that had just SUCCEEDED never existed - the exact lost
            // answer this controller exists to prevent.
            //
            // Retried because Read holds the file with FileShare.Read, which denies
            // delete: a poll landing on the same tick makes Replace throw, and the
            // worker's finally swallows it, losing the RFC's answer for good.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(tmp, path, null);
                    else File.Move(tmp, path);
                    return;
                }
                catch (IOException) when (attempt < 4) { }
                catch (UnauthorizedAccessException) when (attempt < 4) { }
                Thread.Sleep(50);
            }
        }

        private static JObject Read(string id)
        {
            string path = JobPath(id);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    // DateParseHandling.None is NOT optional. Json.NET's default
                    // turns any ISO-8601 string into a Date token, so
                    // submitted_utc stops being the text we wrote: casting it
                    // back with (string) renders a DateTime without its zone,
                    // TryParse then yields Kind=Unspecified, and the
                    // ToUniversalTime() in Poll() subtracts the server's offset
                    // from an instant that was ALREADY UTC. On this IST box that
                    // added exactly 19800s to every job's age, so a job three
                    // seconds old reported "No result after 19807s - the worker
                    // was interrupted", which is the one answer that must never
                    // be wrong: it invites a retry, and a retried variant
                    // creation makes a duplicate article that cannot be deleted.
                    // Keeping every value as the string we stored also means a
                    // record round-trips byte-for-byte.
                    if (File.Exists(path))
                    {
                        using (var sr = new StringReader(File.ReadAllText(path)))
                        using (var jr = new JsonTextReader(sr) { DateParseHandling = DateParseHandling.None })
                        {
                            return JObject.Load(jr);
                        }
                    }
                }
                catch (IOException) { }        // mid-write
                Thread.Sleep(50);              // absent or locked: both are retryable
            }
            return null;
        }

        /// <summary>Drop records older than a day so App_Data cannot grow without
        /// bound. Best effort only - a sweep failure must never fail a submission.</summary>
        private static void Sweep()
        {
            try
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-1);
                foreach (string f in Directory.GetFiles(JobDir(), "*.json"))
                {
                    try { if (File.GetLastWriteTimeUtc(f) < cutoff) File.Delete(f); }
                    catch { }
                }
            }
            catch { }
        }

        private bool IsAuthorized()
        {
            IEnumerable<string> keyHeaders;
            bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
            return hasKey && keyHeaders.FirstOrDefault() == API_KEY;
        }

        /// <summary>
        /// Submit an RFC for background execution. Returns at once - the caller is
        /// never held on the connection, so the edge timeout cannot reach it.
        /// </summary>
        [HttpPost]
        [Route("api/rfc/jobs")]
        public IHttpActionResult Submit([FromBody] JObject body)
        {
            if (!IsAuthorized())
            {
                return Content(HttpStatusCode.Unauthorized, new JObject
                {
                    ["EX_RETURN"] = new JObject
                    {
                        ["TYPE"] = "E",
                        ["MESSAGE"] = "Unauthorized - missing or invalid X-RFC-Key"
                    }
                });
            }

            if (body == null)
            {
                return Content(HttpStatusCode.BadRequest, new JObject
                {
                    ["EX_RETURN"] = new JObject { ["TYPE"] = "E", ["MESSAGE"] = "Request body cannot be null" }
                });
            }

            string rfcName = body.Value<string>("bapiname") ?? "";
            if (string.IsNullOrWhiteSpace(rfcName))
            {
                return Content(HttpStatusCode.BadRequest, new JObject
                {
                    ["EX_RETURN"] = new JObject { ["TYPE"] = "E", ["MESSAGE"] = "bapiname is required" }
                });
            }

            string env = System.Web.HttpContext.Current != null &&
                         System.Web.HttpContext.Current.Request != null
                         ? (System.Web.HttpContext.Current.Request.QueryString["env"] ?? "dev")
                         : "dev";

            // Preserve the caller's own control flags on the forwarded URL so the
            // async path and the sync path invoke the proxy identically.
            string strict = System.Web.HttpContext.Current != null &&
                            System.Web.HttpContext.Current.Request != null
                            ? System.Web.HttpContext.Current.Request.QueryString["strict"]
                            : null;
            string labelRow = System.Web.HttpContext.Current != null &&
                              System.Web.HttpContext.Current.Request != null
                              ? System.Web.HttpContext.Current.Request.QueryString["labelrow"]
                              : null;

            Sweep();

            string id = Guid.NewGuid().ToString("N");
            string submitted = DateTime.UtcNow.ToString("o");

            JObject rec = new JObject
            {
                ["job_id"] = id,
                ["status"] = "running",
                ["rfc_name"] = rfcName,
                ["env"] = env,
                ["submitted_utc"] = submitted,
                ["finished_utc"] = null,
                ["elapsed_ms"] = null,
                ["http_status"] = null,
                ["result"] = null,
                ["error"] = null
            };
            try
            {
                Write(id, rec);
            }
            catch (Exception ex)
            {
                // No store means no way to hand the answer back, and an RFC whose
                // result is unreadable is worse than one never started.
                return Content(HttpStatusCode.InternalServerError, new JObject
                {
                    ["EX_RETURN"] = new JObject
                    {
                        ["TYPE"] = "E",
                        ["MESSAGE"] = "Job store not writable (" + ex.Message +
                                      ") - RFC NOT submitted. Grant the app pool write " +
                                      "access to App_Data/rfcjobs."
                    }
                });
            }

            string url = SelfBaseUrl() + "/api/rfc/proxy?env=" + Uri.EscapeDataString(env);
            if (!string.IsNullOrEmpty(strict)) url += "&strict=" + Uri.EscapeDataString(strict);
            if (!string.IsNullOrEmpty(labelRow)) url += "&labelrow=" + Uri.EscapeDataString(labelRow);

            string payload = body.ToString(Newtonsoft.Json.Formatting.None);

            // QueueBackgroundWorkItem, not a bare thread: it registers the work with
            // ASP.NET so a shutdown waits for it instead of tearing it down mid-RFC.
            try
            {
                HostingEnvironment.QueueBackgroundWorkItem(async ct =>
            {
                DateTime started = DateTime.UtcNow;
                JObject done = Read(id) ?? rec;
                try
                {
                    using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, url))
                    {
                        cts.CancelAfter(WorkerTimeout());
                        req.Headers.TryAddWithoutValidation("X-RFC-Key", API_KEY);
                        req.Content = new StringContent(payload, new UTF8Encoding(false), "application/json");

                        using (HttpResponseMessage resp = await Http.SendAsync(req, cts.Token).ConfigureAwait(false))
                        {
                            string text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                            done["http_status"] = (int)resp.StatusCode;

                            // The proxy answers JSON. Anything else is stored raw
                            // rather than discarded - an unparseable answer is
                            // still evidence.
                            JToken parsed;
                            try { parsed = JToken.Parse(text); }
                            catch { parsed = new JObject { ["raw"] = text }; }

                            done["result"] = parsed;

                            string sapType, sapMessage;
                            ReadSapVerdict(parsed, LabelRowExpected(labelRow),
                                           out sapType, out sapMessage);
                            done["sap_type"] = sapType;

                            bool httpOk = resp.IsSuccessStatusCode;
                            bool sapOk = !IsFailureType(sapType);

                            done["status"] = (httpOk && sapOk) ? "done" : "failed";
                            if (!httpOk)
                            {
                                done["error"] = "proxy returned HTTP " + (int)resp.StatusCode +
                                    (sapMessage.Length > 0 ? " - " + sapMessage : "");
                            }
                            else if (!sapOk)
                            {
                                done["error"] = sapMessage.Length > 0
                                    ? sapMessage
                                    : "SAP returned EX_RETURN TYPE=" + sapType + " with no message";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // The RFC may well have completed in SAP regardless - say so
                    // rather than implying nothing happened.
                    done["status"] = "failed";
                    done["error"] = ex.Message +
                        " - the RFC may still have completed in SAP; verify before retrying.";
                }
                finally
                {
                    done["finished_utc"] = DateTime.UtcNow.ToString("o");
                    done["elapsed_ms"] = (long)(DateTime.UtcNow - started).TotalMilliseconds;
                    try { Write(id, done); } catch { }
                }
                });
            }
            catch (Exception qex)
            {
                // ASP.NET is shutting down, or this is not a hosted context. The
                // RFC never ran, so say so - a record stuck on "running" would be
                // polled for ever.
                rec["status"] = "failed";
                rec["error"] = "Could not queue the RFC: " + qex.Message + " - it was NOT sent to SAP.";
                rec["finished_utc"] = DateTime.UtcNow.ToString("o");
                try { Write(id, rec); } catch { }

                // Every other error path in this app answers with EX_RETURN and the
                // existing consumers branch on its presence, so a bare record would
                // be the single failure they cannot read.
                JObject refused = (JObject)rec.DeepClone();
                refused["EX_RETURN"] = new JObject
                {
                    ["TYPE"] = "E",
                    ["MESSAGE"] = (string)rec["error"]
                };
                return Content(HttpStatusCode.ServiceUnavailable, refused);
            }

            JObject accepted = new JObject
            {
                ["job_id"] = id,
                ["status"] = "running",
                ["rfc_name"] = rfcName,
                ["env"] = env,
                ["submitted_utc"] = submitted,
                ["poll"] = "/api/rfc/jobs/" + id,
                ["EX_RETURN"] = new JObject
                {
                    ["TYPE"] = "S",
                    ["MESSAGE"] = "Accepted - poll /api/rfc/jobs/" + id + " for the result"
                }
            };
            return Content(HttpStatusCode.Accepted, accepted);
        }

        /// <summary>Read a submitted job. 'running' means exactly that: still in
        /// flight, or interrupted by an app-pool recycle. It never means failed.</summary>
        [HttpGet]
        [Route("api/rfc/jobs/{id}")]
        public IHttpActionResult Status(string id)
        {
            if (!IsAuthorized())
            {
                return Content(HttpStatusCode.Unauthorized, new JObject
                {
                    ["EX_RETURN"] = new JObject
                    {
                        ["TYPE"] = "E",
                        ["MESSAGE"] = "Unauthorized - missing or invalid X-RFC-Key"
                    }
                });
            }

            // The id is used to build a file path; accept only what we generate.
            bool clean = !string.IsNullOrEmpty(id) && id.Length == 32 &&
                         id.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
            if (!clean)
            {
                return Content(HttpStatusCode.BadRequest, new JObject
                {
                    ["EX_RETURN"] = new JObject { ["TYPE"] = "E", ["MESSAGE"] = "Malformed job id" }
                });
            }

            JObject rec = Read(id);
            if (rec != null && (string)rec["status"] == "running")
            {
                // V2RfcTestPool is recycled on every deploy and by the NCo
                // watchdog, which kills the in-flight worker without ever
                // updating its record. Past the worker's own ceiling, "running"
                // is no longer true - and silence is the worst answer here.
                DateTime submittedAt;
                if (DateTime.TryParse((string)rec["submitted_utc"],
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out submittedAt))
                {
                    TimeSpan age = DateTime.UtcNow - submittedAt.ToUniversalTime();
                    if (age > WorkerTimeout() + TimeSpan.FromSeconds(60))
                    {
                        rec = (JObject)rec.DeepClone();
                        rec["status"] = "unknown";
                        rec["error"] = "No result after " + (int)age.TotalSeconds +
                            "s - the worker was interrupted (app-pool recycle). SAP may " +
                            "have completed this RFC; verify before retrying.";
                    }
                }
            }
            if (rec == null)
            {
                return Content(HttpStatusCode.NotFound, new JObject
                {
                    ["EX_RETURN"] = new JObject
                    {
                        ["TYPE"] = "E",
                        ["MESSAGE"] = "No such job " + id + " - records are kept for 24 hours"
                    }
                });
            }

            return Content(HttpStatusCode.OK, rec);
        }
    }
}
