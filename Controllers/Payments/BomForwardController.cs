using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace Vendor_SRM_Routing_Application.Controllers.Payments
{
    /// <summary>
    /// Bank of Maharashtra (MahaPay) egress relay.
    ///
    /// BOM whitelists ONE source IP for production API calls — the office
    /// static 103.29.220.152, which is what this server egresses from. The
    /// payment system runs on Supabase Edge Functions (AWS Sydney) and has no
    /// static egress IP, so BOM's production gateway answers those calls with
    /// 403. The edge function therefore posts the BOM-bound request here and
    /// this endpoint forwards it, so BOM sees the whitelisted IP.
    ///
    /// POST /api/bom/forward
    ///   Headers: X-Relay-Key (auth)
    ///   Body:    { base_url, path, method, headers{}, body }
    ///   Returns: 200 { status, headers, body, elapsed_ms }  = BOM was reached,
    ///            and `status` is whatever BOM answered (403 and 500 included).
    ///            Non-200 = the relay itself failed; BOM was never reached.
    ///
    /// This is deliberately NOT a general proxy. base_url is matched against a
    /// compile-time allowlist holding exactly the production host, the caller
    /// supplies only a path under /v1/ or /v2/, the method is GET or POST, and
    /// exactly four request headers are forwarded. Nothing else the caller
    /// sends reaches the bank. UAT is never relayed — it has no IP
    /// restriction and the payment functions call it directly.
    ///
    /// Bodies carry encrypted banking data and are never logged. The ring
    /// buffer below records path, status, timing and caller IP only, and the
    /// global RequestLoggingHandler records no bodies either.
    /// </summary>
    [RoutePrefix("api/bom")]
    public class BomForwardController : ApiController
    {
        // Production MahaPay. The caller may name a base_url, but it is matched
        // against ALLOWED_HOSTS by exact string, so the only way to point the
        // relay somewhere else is to edit this file. UAT deliberately absent:
        // it carries no IP restriction, so it is called directly and must not
        // be reachable through a relay that exists to spend our whitelisted IP.
        private const string BOM_HOST = "https://mahaapi.bankofmaharashtra.bank.in";
        private static readonly string[] ALLOWED_HOSTS = { BOM_HOST };

        private const string KEY_HEADER = "X-Relay-Key";
        private const string KEY_SETTING = "BOM_RELAY_KEY";

        private const int MAX_BODY_BYTES = 1000000;   // ~1 MB; BOM payloads are a few KB
        private const int MAX_PATH_LENGTH = 512;
        private const int UPSTREAM_TIMEOUT_SEC = 60;

        // Auth-Token carries BOM's MahaPay session token on the submit and
        // status APIs, so it is forwarded alongside Authorization.
        private static readonly string[] ALLOWED_HEADERS = { "Content-Type", "Authorization", "Auth-Token", "channel" };
        private static readonly string[] ALLOWED_METHODS = { "GET", "POST" };
        private static readonly string[] ALLOWED_PATH_PREFIXES = { "/v1/", "/v2/" };

        // One static client for the process. A per-request HttpClient leaks
        // sockets in TIME_WAIT and, on a payment path, that surfaces as random
        // "upstream unreachable" long after the code that caused it.
        private static readonly HttpClient Http;

        static BomForwardController()
        {
            // .NET 4.x does not negotiate TLS 1.2 unless told to on every
            // framework/OS pairing this app has been seen on. Bank gateways
            // refuse anything older.
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { /* older framework build without the enum member */ }

            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(UPSTREAM_TIMEOUT_SEC) };
        }

        // ── POST /api/bom/forward ────────────────────────────────────────
        [HttpPost]
        [Route("forward")]
        public async Task<IHttpActionResult> Forward()
        {
            var sw = Stopwatch.StartNew();
            string callerIp = GetCallerIp();

            // 1. Auth. Fails closed when the host has no key configured —
            //    an unset secret must never mean "open relay to the bank".
            string expected = ReadSetting(KEY_SETTING);
            if (string.IsNullOrWhiteSpace(expected))
            {
                Record("-", "-", 0, sw, callerIp, "not_configured");
                return Content(HttpStatusCode.ServiceUnavailable, new
                {
                    error = "relay key not configured",
                    message = KEY_SETTING + " is not set on this host, so the relay cannot " +
                              "authenticate callers and refuses to forward. Set it as a machine " +
                              "environment variable or in secrets.config — never in Web.config, " +
                              "which is force-copied from this public repo on every deploy."
                });
            }

            IEnumerable<string> keyHeaders;
            string presented = Request.Headers.TryGetValues(KEY_HEADER, out keyHeaders)
                ? (keyHeaders.FirstOrDefault() ?? "")
                : "";
            if (!FixedTimeEquals(presented, expected))
            {
                Record("-", "-", 0, sw, callerIp, "unauthorized");
                return Content(HttpStatusCode.Unauthorized, new { error = "invalid relay key" });
            }

            // 2. Size gate before the body is read into memory. httpRuntime
            //    allows 2 GB globally; checking only after ReadAsStringAsync
            //    means a hostile caller has already been given the memory.
            long? declared = Request.Content.Headers.ContentLength;
            if (declared.HasValue && declared.Value > MAX_BODY_BYTES * 2)
            {
                Record("-", "-", 0, sw, callerIp, "envelope_too_large");
                return Content(HttpStatusCode.BadRequest, new { error = "bad request body" });
            }

            ForwardRequest req;
            try
            {
                string raw = await Request.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    Record("-", "-", 0, sw, callerIp, "empty_body");
                    return Content(HttpStatusCode.BadRequest, new { error = "bad request body" });
                }
                req = JsonConvert.DeserializeObject<ForwardRequest>(raw);
            }
            catch (Exception)
            {
                // The parse error text can quote the payload, so it is dropped
                // rather than echoed or logged.
                Record("-", "-", 0, sw, callerIp, "unparseable_body");
                return Content(HttpStatusCode.BadRequest, new { error = "bad request body" });
            }

            if (req == null || string.IsNullOrWhiteSpace(req.path))
            {
                Record("-", "-", 0, sw, callerIp, "no_path");
                return Content(HttpStatusCode.BadRequest, new { error = "bad request body" });
            }

            // base_url is matched by exact string. Omitted or blank means the
            // production host, which keeps the first spec's payload shape
            // working — the default is the only allowlisted value either way.
            string baseUrl = string.IsNullOrWhiteSpace(req.base_url)
                ? BOM_HOST
                : req.base_url.Trim();
            if (!ALLOWED_HOSTS.Contains(baseUrl, StringComparer.Ordinal))
            {
                Record("-", "-", 0, sw, callerIp, "host_not_allowed");
                return Content(HttpStatusCode.BadRequest, new { error = "host not allowed" });
            }

            string path = req.path.Trim();
            if (!IsPathAllowed(path))
            {
                Record(path, "-", 0, sw, callerIp, "path_not_allowed");
                return Content(HttpStatusCode.BadRequest, new { error = "path not allowed" });
            }

            string method = (req.method ?? "POST").Trim().ToUpperInvariant();
            if (!ALLOWED_METHODS.Contains(method))
            {
                Record(path, method, 0, sw, callerIp, "method_not_allowed");
                return Content(HttpStatusCode.BadRequest, new { error = "method not allowed" });
            }

            string body = req.body ?? "";
            if (Encoding.UTF8.GetByteCount(body) > MAX_BODY_BYTES)
            {
                Record(path, method, 0, sw, callerIp, "body_too_large");
                return Content(HttpStatusCode.BadRequest, new { error = "body too large" });
            }

            // 3. Build the upstream request. The host is the allowlisted value
            //    checked above; only allowlisted headers survive. Host, Cookie,
            //    X-Forwarded-*, X-Relay-Key and everything else the caller sent
            //    are dropped here.
            var upstream = new HttpRequestMessage(new HttpMethod(method), baseUrl + path);
            string contentType = "application/json";

            if (req.headers != null)
            {
                foreach (var h in req.headers)
                {
                    if (h.Key == null) continue;
                    if (!ALLOWED_HEADERS.Contains(h.Key, StringComparer.OrdinalIgnoreCase)) continue;
                    if (h.Value == null) continue;

                    if (h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        contentType = h.Value;              // applied to the content below
                    else
                        upstream.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }

            if (method == "POST")
            {
                // StringContent(body, encoding, mediaType) appends
                // "; charset=utf-8". BOM's token endpoint is form-urlencoded
                // and gateways of that vintage reject the parameter, so the
                // caller's Content-Type is set verbatim instead.
                var content = new StringContent(body, new UTF8Encoding(false));
                try
                {
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                }
                catch (FormatException)
                {
                    Record(path, method, 0, sw, callerIp, "bad_content_type");
                    return Content(HttpStatusCode.BadRequest, new { error = "bad request body" });
                }
                upstream.Content = content;
            }

            // 4. Send, and wrap whatever comes back.
            try
            {
                using (var resp = await Http.SendAsync(upstream))
                {
                    string respBody = await resp.Content.ReadAsStringAsync();
                    int status = (int)resp.StatusCode;
                    sw.Stop();
                    Record(path, method, status, sw, callerIp, null);

                    var headers = new Dictionary<string, string>();
                    if (resp.Content.Headers.ContentType != null)
                        headers["Content-Type"] = resp.Content.Headers.ContentType.ToString();

                    // 200 from the relay means "BOM answered". BOM's own status
                    // is inside, so the caller can tell a bank refusal apart
                    // from a network failure.
                    return Ok(new
                    {
                        status = status,
                        headers = headers,
                        body = respBody,
                        elapsed_ms = sw.ElapsedMilliseconds
                    });
                }
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                Record(path, method, 0, sw, callerIp, "timeout");
                return Content(HttpStatusCode.GatewayTimeout, new
                {
                    error = "upstream timeout after " + UPSTREAM_TIMEOUT_SEC + "s"
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                // Exception text here is connect/TLS/DNS detail about our own
                // call to BOM. It carries no payload.
                string detail = ex.Message;
                if (ex.InnerException != null) detail += " | " + ex.InnerException.Message;
                Record(path, method, 0, sw, callerIp, "unreachable");
                return Content(HttpStatusCode.BadGateway, new
                {
                    error = "upstream unreachable: " + detail
                });
            }
        }

        // ── GET /api/bom/health ──────────────────────────────────────────
        /// <summary>
        /// Deploy check. Unauthenticated callers learn only that the endpoint
        /// is live; presenting the key adds the configuration detail.
        /// </summary>
        [HttpGet]
        [Route("health")]
        public IHttpActionResult Health()
        {
            if (!KeyOk())
                return Ok(new { status = true, service = "bom-forward" });

            return Ok(new
            {
                status = true,
                service = "bom-forward",
                upstream = BOM_HOST,
                key_configured = true,
                allowed_hosts = ALLOWED_HOSTS,
                allowed_path_prefixes = ALLOWED_PATH_PREFIXES,
                allowed_methods = ALLOWED_METHODS,
                allowed_headers = ALLOWED_HEADERS,
                max_body_bytes = MAX_BODY_BYTES,
                timeout_seconds = UPSTREAM_TIMEOUT_SEC,
                log_dir = LogDir(),
                log_durable = Directory.Exists(LogDir()) && _lastWriteError == null,
                log_write_error = _lastWriteError,
                log_retention_days = MAX_LOG_DAYS,
                server = Environment.MachineName
            });
        }

        // ── GET /api/bom/egress-ip ───────────────────────────────────────
        /// <summary>
        /// Reports the public IP this server actually leaves from, so the BOM
        /// whitelist can be confirmed before any payment traffic is wired up.
        /// The URL is a constant and no caller input reaches it.
        /// </summary>
        [HttpGet]
        [Route("egress-ip")]
        public async Task<IHttpActionResult> EgressIp()
        {
            if (!KeyOk())
                return Content(HttpStatusCode.Unauthorized, new { error = "invalid relay key" });

            const string ECHO_URL = "https://api.ipify.org?format=json";
            const string EXPECTED = "103.29.220.152";

            try
            {
                using (var resp = await Http.GetAsync(ECHO_URL))
                {
                    string raw = await resp.Content.ReadAsStringAsync();
                    string ip = "";
                    try { ip = (string)Newtonsoft.Json.Linq.JObject.Parse(raw)["ip"]; } catch { }
                    return Ok(new
                    {
                        status = true,
                        egress_ip = ip,
                        expected = EXPECTED,
                        whitelisted = string.Equals(ip, EXPECTED, StringComparison.Ordinal),
                        server = Environment.MachineName,
                        source = ECHO_URL
                    });
                }
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadGateway, new
                {
                    status = false,
                    error = "echo service unreachable: " + ex.Message
                });
            }
        }

        // ── GET /api/bom/log ─────────────────────────────────────────────
        /// <summary>
        /// Recent relay calls: path, method, BOM status, timing, caller IP.
        /// No request or response bodies, and no Authorization header, are
        /// held anywhere — not in the buffer and not in the file.
        ///
        /// Reads the daily JSONL files first, so the history survives an app
        /// pool recycle. The in-memory ring is the fallback for a host where
        /// the directory cannot be written, and `durable` says which one
        /// answered — a log that quietly forgets is worse than one that
        /// admits it.
        /// </summary>
        [HttpGet]
        [Route("log")]
        public IHttpActionResult Log(int limit = 50, int days = 7)
        {
            if (!KeyOk())
                return Content(HttpStatusCode.Unauthorized, new { error = "invalid relay key" });

            if (limit < 1) limit = 1;
            if (limit > 1000) limit = 1000;
            if (days < 1) days = 1;
            if (days > MAX_LOG_DAYS) days = MAX_LOG_DAYS;

            List<BomLogEntry> result;
            bool durable = false;

            var fromDisk = ReadFromDisk(limit, days);
            if (fromDisk != null)
            {
                result = fromDisk;
                durable = true;
            }
            else
            {
                var entries = _log.ToArray();
                result = new List<BomLogEntry>();
                for (int i = entries.Length - 1; i >= 0 && result.Count < limit; i--)
                    result.Add(entries[i]);
            }

            return Ok(new
            {
                status = true,
                durable = durable,
                source = durable ? LogDir() : "memory (ring buffer, lost on pool recycle)",
                write_error = _lastWriteError,
                count = result.Count,
                entries = result
            });
        }

        // ── Shared ───────────────────────────────────────────────────────

        private bool KeyOk()
        {
            string expected = ReadSetting(KEY_SETTING);
            if (string.IsNullOrWhiteSpace(expected)) return false;

            IEnumerable<string> keyHeaders;
            string presented = Request.Headers.TryGetValues(KEY_HEADER, out keyHeaders)
                ? (keyHeaders.FirstOrDefault() ?? "")
                : "";
            return FixedTimeEquals(presented, expected);
        }

        /// <summary>
        /// A path is allowed only if it is a plain absolute path under /v1/ or
        /// /v2/. "//host/x" would make the Uri class resolve a different
        /// authority, and "/v1/../x" normalises out of the allowlisted prefix,
        /// so both are refused rather than sanitised.
        /// </summary>
        private static bool IsPathAllowed(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.Length > MAX_PATH_LENGTH) return false;
            if (!path.StartsWith("/", StringComparison.Ordinal)) return false;
            if (path.StartsWith("//", StringComparison.Ordinal)) return false;
            if (path.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            if (path.IndexOf('\\') >= 0) return false;
            if (path.IndexOf('@') >= 0) return false;

            foreach (char c in path)
                if (char.IsControl(c) || c == ' ') return false;

            return ALLOWED_PATH_PREFIXES.Any(p => path.StartsWith(p, StringComparison.Ordinal));
        }

        /// <summary>
        /// Length-independent comparison, so a wrong key cannot be recovered a
        /// character at a time from response timing.
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
        /// live in Web.config itself — and this repo is public.
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

        /// <summary>
        /// Cloudflare fronts this host, so UserHostAddress is an edge IP. The
        /// forwarded-for headers are read for the log only — they are never
        /// passed upstream to BOM.
        /// </summary>
        private string GetCallerIp()
        {
            try
            {
                IEnumerable<string> vals;
                if (Request.Headers.TryGetValues("CF-Connecting-IP", out vals))
                {
                    string v = vals.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
                if (Request.Headers.TryGetValues("X-Forwarded-For", out vals))
                {
                    string v = vals.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(v)) return v.Split(',')[0].Trim();
                }

                object ctx;
                if (Request.Properties.TryGetValue("MS_HttpContext", out ctx))
                {
                    var httpCtx = ctx as HttpContextWrapper;
                    if (httpCtx != null && httpCtx.Request != null)
                        return httpCtx.Request.UserHostAddress ?? "";
                }
            }
            catch { }
            return "";
        }

        // ── The log. Metadata only: never a body, never Authorization ──────
        //
        // One JSONL file per UTC day under LogDir(), plus an in-memory ring as
        // the fallback when that directory cannot be written. The file is what
        // makes the history outlive an app pool recycle — the ring alone does
        // not, which is exactly how the first six calls of 1-Sep-2026 were
        // lost between a test and the read that went looking for them.
        //
        // The directory must sit OUTSIDE the site root: C:\V2RfcTest is build
        // output and deploy-iis.yml writes over it.
        private const int MaxLogEntries = 200;
        private const int MAX_LOG_DAYS = 30;
        private const string LOG_DIR_SETTING = "BOM_LOG_DIR";
        private const string DEFAULT_LOG_DIR = @"C:\V2RfcState\bom-relay";

        private static readonly ConcurrentQueue<BomLogEntry> _log = new ConcurrentQueue<BomLogEntry>();
        private static readonly object _fileLock = new object();
        private static string _lastWriteError;
        private static DateTime _lastPruneUtc = DateTime.MinValue;

        private static string LogDir()
        {
            string dir = ReadSetting(LOG_DIR_SETTING);
            return string.IsNullOrWhiteSpace(dir) ? DEFAULT_LOG_DIR : dir;
        }

        private static string LogPath(DateTime utcDay)
        {
            return Path.Combine(LogDir(), "bom-relay-" + utcDay.ToString("yyyyMMdd") + ".jsonl");
        }

        private static void Record(string path, string method, int bomStatus, Stopwatch sw,
                                   string callerIp, string error)
        {
            var entry = new BomLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Path = path,
                Method = method,
                BomStatus = bomStatus,
                ElapsedMs = sw.ElapsedMilliseconds,
                CallerIp = callerIp,
                Error = error
            };

            _log.Enqueue(entry);
            BomLogEntry drop;
            while (_log.Count > MaxLogEntries) _log.TryDequeue(out drop);

            // A logging failure must never take the payment path down with it.
            try
            {
                string dir = LogDir();
                lock (_fileLock)
                {
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(LogPath(entry.Timestamp),
                                       JsonConvert.SerializeObject(entry) + Environment.NewLine,
                                       new UTF8Encoding(false));
                    _lastWriteError = null;
                }
                Prune();
            }
            catch (Exception ex)
            {
                _lastWriteError = ex.Message;
            }
        }

        /// <summary>
        /// Reads newest-first across the last `days` daily files. Returns null
        /// when the directory is unreadable, which is the signal to fall back
        /// to the ring rather than to report an empty history as fact.
        /// </summary>
        private static List<BomLogEntry> ReadFromDisk(int limit, int days)
        {
            try
            {
                string dir = LogDir();
                if (!Directory.Exists(dir)) return null;

                var result = new List<BomLogEntry>();
                DateTime day = DateTime.UtcNow.Date;

                for (int d = 0; d < days && result.Count < limit; d++, day = day.AddDays(-1))
                {
                    string file = LogPath(day);
                    if (!File.Exists(file)) continue;

                    string[] lines;
                    lock (_fileLock) { lines = File.ReadAllLines(file); }

                    for (int i = lines.Length - 1; i >= 0 && result.Count < limit; i--)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        try { result.Add(JsonConvert.DeserializeObject<BomLogEntry>(lines[i])); }
                        catch { /* a torn last line is not a reason to lose the rest */ }
                    }
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Drops files older than MAX_LOG_DAYS. Runs at most daily.</summary>
        private static void Prune()
        {
            if ((DateTime.UtcNow - _lastPruneUtc).TotalHours < 24) return;
            _lastPruneUtc = DateTime.UtcNow;
            try
            {
                var cutoff = DateTime.UtcNow.Date.AddDays(-MAX_LOG_DAYS);
                foreach (string f in Directory.GetFiles(LogDir(), "bom-relay-*.jsonl"))
                {
                    string stamp = Path.GetFileNameWithoutExtension(f).Replace("bom-relay-", "");
                    DateTime when;
                    if (DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture,
                                               DateTimeStyles.None, out when) && when < cutoff)
                        File.Delete(f);
                }
            }
            catch { /* housekeeping is not worth an exception on the payment path */ }
        }
    }

    public class ForwardRequest
    {
        public string base_url { get; set; }
        public string path { get; set; }
        public string method { get; set; }
        public Dictionary<string, string> headers { get; set; }
        public string body { get; set; }
    }

    public class BomLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Path { get; set; }
        public string Method { get; set; }
        public int BomStatus { get; set; }
        public long ElapsedMs { get; set; }
        public string CallerIp { get; set; }
        public string Error { get; set; }
    }
}
