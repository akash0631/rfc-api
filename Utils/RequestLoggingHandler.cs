using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Vendor_SRM_Routing_Application.Utils
{
    /// <summary>
    /// HTTP message handler that logs every API request with timing, status and RFC name.
    /// Maintains a thread-safe ring buffer of the last 1,000 requests.
    /// Accessible via GET /api/request-log
    /// </summary>
    public class RequestLoggingHandler : DelegatingHandler
    {
        private const int MaxEntries = 1000;
        private static readonly ConcurrentQueue<RequestLogEntry> _log = new ConcurrentQueue<RequestLogEntry>();
        private static long _totalRequests = 0;
        private static long _totalErrors = 0;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            HttpResponseMessage response = null;
            string errorMsg = null;

            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                throw;
            }
            finally
            {
                sw.Stop();
                var entry = new RequestLogEntry
                {
                    Timestamp   = DateTime.UtcNow,
                    Method      = request.Method.Method,
                    Path        = request.RequestUri?.AbsolutePath ?? "",
                    RfcName     = ExtractRfcName(request.RequestUri?.AbsolutePath),
                    StatusCode  = response?.StatusCode.GetHashCode() ?? 0,
                    DurationMs  = (int)sw.ElapsedMilliseconds,
                    ClientIp    = GetClientIp(request),
                    Error       = errorMsg
                };

                // Ring buffer — remove oldest if full
                _log.Enqueue(entry);
                while (_log.Count > MaxEntries) _log.TryDequeue(out _);

                Interlocked.Increment(ref _totalRequests);
                if (entry.StatusCode >= 500 || errorMsg != null)
                    Interlocked.Increment(ref _totalErrors);
            }

            return response;
        }

        private static string ExtractRfcName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            // /api/RfcName or /api/execute/RfcCode
            var parts = path.TrimStart('/').Split('/');
            if (parts.Length >= 2 && parts[0].ToLower() == "api")
                return parts.Length >= 3 && parts[1].ToLower() == "execute" ? parts[2] : parts[1];
            return "";
        }

        private static string GetClientIp(HttpRequestMessage request)
        {
            if (request.Properties.TryGetValue("MS_HttpContext", out var ctx))
            {
                var httpCtx = ctx as HttpContextWrapper;
                return httpCtx?.Request?.UserHostAddress ?? "";
            }
            return "";
        }

        // ── Static accessors ──────────────────────────────────────────────
        public static IEnumerable<RequestLogEntry> GetRecentLog(int limit = 100)
        {
            var entries = _log.ToArray();
            var result = new List<RequestLogEntry>();
            for (int i = entries.Length - 1; i >= 0 && result.Count < limit; i--)
                result.Add(entries[i]);
            return result;
        }

        public static object GetStats()
        {
            var entries = _log.ToArray();
            long total = Interlocked.Read(ref _totalRequests);
            long errors = Interlocked.Read(ref _totalErrors);
            var rfcCounts = new Dictionary<string, int>();
            var slowest = 0;

            foreach (var e in entries)
            {
                if (!string.IsNullOrEmpty(e.RfcName))
                {
                    if (!rfcCounts.ContainsKey(e.RfcName)) rfcCounts[e.RfcName] = 0;
                    rfcCounts[e.RfcName]++;
                }
                if (e.DurationMs > slowest) slowest = e.DurationMs;
            }

            return new
            {
                TotalRequests   = total,
                TotalErrors     = errors,
                ErrorRate       = total > 0 ? Math.Round((double)errors / total * 100, 1) : 0,
                SlowestMs       = slowest,
                BufferedEntries = entries.Length,
                TopRfcs         = rfcCounts
            };
        }
    }

    public class RequestLogEntry
    {
        public DateTime Timestamp  { get; set; }
        public string  Method     { get; set; }
        public string  Path       { get; set; }
        public string  RfcName    { get; set; }
        public int     StatusCode  { get; set; }
        public int     DurationMs  { get; set; }
        public string  ClientIp   { get; set; }
        public string  Error      { get; set; }
    }
}
