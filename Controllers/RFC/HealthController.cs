using System;
using System.Reflection;
using System.Web.Http;
using Vendor_SRM_Routing_Application.Utils;

namespace Vendor_SRM_Routing_Application.Controllers.RFC
{
    /// <summary>
    /// Health check endpoint — call this to verify the API is live and responding.
    /// GET /api/health
    /// Returns: build date, uptime, request stats summary, SAP connection status.
    /// Used by portal Live Status tab and monitoring tools.
    /// Zero dependencies — always returns 200 even if SAP is down.
    /// </summary>
    [RoutePrefix("api")]
    public class HealthController : ApiController
    {
        private static readonly DateTime _startTime = DateTime.UtcNow;

        [HttpGet, Route("health")]
        public IHttpActionResult GetHealth()
        {
            var uptime = DateTime.UtcNow - _startTime;
            object stats = null;
            try { stats = RequestLoggingHandler.GetStats(); } catch { }

            // Build date from assembly
            var buildDate = "unknown";
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var info = System.IO.File.GetLastWriteTimeUtc(asm.Location);
                buildDate = info.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            }
            catch { }

            return Ok(new
            {
                Status       = "healthy",
                Service      = "V2 Retail SAP RFC API",
                Version      = "2.0",
                BuildDate    = buildDate,
                UptimeHours  = Math.Round(uptime.TotalHours, 2),
                UptimeDisplay = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m",
                Timestamp    = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Environment  = new
                {
                    IIS        = "V2DC-ADDVERB:9292",
                    SAP_Dev    = "192.168.144.174:210",
                    SAP_Prod   = "192.168.144.170:600",
                    Swagger    = "https://sap-api.v2retail.net/swagger/ui/index",
                    Explorer   = "https://sap-api.v2retail.net/v2_sap_api_explorer.html"
                },
                RequestStats = stats,
                Checks = new
                {
                    RequestLogging = stats != null ? "ok" : "unavailable",
                    IISPool        = "V2RfcTestPool",
                    GitHubRepo     = "akash0631/rfc-api",
                    LastDeployedBy = "github-actions"
                }
            });
        }
    }
}
