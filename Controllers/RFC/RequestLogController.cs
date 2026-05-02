using System.Web.Http;
using Vendor_SRM_Routing_Application.Utils;  // RequestLoggingHandler is here

namespace Vendor_SRM_Routing_Application.Controllers.RFC
{
    /// <summary>
    /// Returns recent API request log and statistics from the in-memory ring buffer.
    /// GET /api/request-log         — last 100 requests, newest first
    /// GET /api/request-log?limit=N — last N requests (max 500)
    /// GET /api/request-log/stats   — totals: request count, error rate, slowest RFC, top RFCs
    /// Data resets on IIS recycle — for persistent history use Snowflake GOLD.RFC_API_ACCESS_LOG
    /// </summary>
    [RoutePrefix("api")]
    public class RequestLogController : ApiController
    {
        [HttpGet, Route("request-log")]
        public IHttpActionResult GetLog([FromUri] int limit = 100)
        {
            if (limit < 1)   limit = 1;
            if (limit > 500) limit = 500;
            return Ok(new {
                Status  = "S",
                Message = "Request log — newest first. Resets on IIS recycle.",
                Data    = RequestLoggingHandler.GetRecentLog(limit)
            });
        }

        [HttpGet, Route("request-log/stats")]
        public IHttpActionResult GetStats()
        {
            return Ok(new {
                Status  = "S",
                Message = "Aggregate request statistics since last IIS recycle",
                Data    = RequestLoggingHandler.GetStats()
            });
        }
    }
}
