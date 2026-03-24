using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.HHT
{
    /// <summary>
    /// Single entry point for ALL Android HHT device requests.
    ///
    /// Android sends:  POST /api/hht
    ///                 Body:  opcode#param1#param2#...   (raw text, UTF-8)
    ///                 Headers: X-AppVersion: Store;Android;5.0  (optional, for logging)
    ///                          X-HHT-Env: QA  (optional, forces QA SAP — dev/test only)
    ///
    /// Response:       S#message  or  E#message  (same protocol as old xmwgw)
    ///
    /// Android app migration: ONLY change the base URL.
    ///   OLD: http://&lt;server200&gt;:8080/xmwgw/ValueXMW/Store/Android/5.0
    ///   NEW: https://sap-api.v2retail.net/api/hht
    ///
    /// Opcode list: see HHTRouter.cs
    /// </summary>
    [RoutePrefix("api/hht")]
    public class HHTController : BaseController
    {
        [HttpPost]
        [Route("")]
        public async Task<HttpResponseMessage> Handle()
        {
            string rawBody  = "";
            string response = "E#Server error";

            try
            {
                // ── Read raw body ──────────────────────────────────────────────
                rawBody = await Request.Content.ReadAsStringAsync();
                rawBody = rawBody?.Trim() ?? "";

                if (string.IsNullOrEmpty(rawBody))
                    return BuildResponse("E#Empty request");

                // ── Extract opcode (first # segment) ──────────────────────────
                int sep    = rawBody.IndexOf('#');
                string op  = (sep > 0 ? rawBody.Substring(0, sep) : rawBody).ToLower().Trim();

                if (string.IsNullOrEmpty(op))
                    return BuildResponse("E#Missing opcode");

                // ── Log (opcode + client IP, no PII) ──────────────────────────
                string clientIp = GetClientIp();
                string appVer   = Request.Headers.Contains("X-AppVersion")
                                  ? string.Join(",", Request.Headers.GetValues("X-AppVersion"))
                                  : "unknown";
                System.Diagnostics.Debug.WriteLine(
                    $"[HHT] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | op={op} | ip={clientIp} | ver={appVer}");

                // ── Resolve handler ────────────────────────────────────────────
                HHTBaseHandler handler = HHTRouter.Resolve(op);
                if (handler == null)
                    return BuildResponse($"E#Unknown opcode: {op}");

                // ── Execute (async, with timeout guard) ───────────────────────
                response = await Task.Run(() =>
                {
                    handler.SetRequest(rawBody);
                    return handler.Execute();
                }).TimeoutAfter(TimeSpan.FromSeconds(60));
            }
            catch (TimeoutException)
            {
                response = "E#SAP request timed out. Please retry.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HHT] EXCEPTION op={rawBody?.Split('#')[0]} : {ex.Message}");
                response = "E#" + ex.Message;
            }

            return BuildResponse(response);
        }

        // ── Health check ──────────────────────────────────────────────────────
        // GET /api/hht/health  — used by CI smoke test and Azure monitoring
        [HttpGet]
        [Route("health")]
        public HttpResponseMessage Health()
        {
            var opcodeCount = HHTRouter.AllOpcodes().Count();
            return BuildResponse($"OK|v2-hht-api|opcodes={opcodeCount}|{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}UTC");
        }

        // ── Backward-compat route: /ValueXMW/* → same handler ─────────────────
        // Allows devices to keep the old URL during cutover period.
        [HttpPost]
        [Route("~/ValueXMW/{appName}/{platform}/{version}")]
        public async Task<HttpResponseMessage> LegacyHandle(string appName, string platform, string version)
        {
            // Just forward to the main handler — protocol is identical
            return await Handle();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static HttpResponseMessage BuildResponse(string body)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new StringContent(body, Encoding.UTF8, "text/plain");
            return resp;
        }

        private string GetClientIp()
        {
            try
            {
                if (Request.Properties.ContainsKey("MS_HttpContext"))
                {
                    dynamic ctx = Request.Properties["MS_HttpContext"];
                    return ctx?.Request?.UserHostAddress ?? "unknown";
                }
            }
            catch { }
            return "unknown";
        }
    }

    // ── Tiny timeout extension ─────────────────────────────────────────────────
    internal static class TaskExtensions
    {
        public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
        {
            var delay = Task.Delay(timeout);
            var first = await Task.WhenAny(task, delay);
            if (first == delay)
                throw new TimeoutException();
            return await task;
        }
    }
}
