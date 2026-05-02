using System.Web.Http;
using Vendor_SRM_Routing_Application.Utils;

namespace Vendor_SRM_Routing_Application.Controllers.RFC
{
    /// <summary>
    /// Returns recent API request log and statistics.
    /// GET /api/request-log         — last 100 requests
    /// GET /api/request-log/stats   — aggregate stats (total, error rate, top RFCs, slowest)
    /// GET /api/request-log?limit=N — last N requests (max 500)
    /// </summary>
    [RoutePrefix("api")]
    public class RequestLogController : ApiController
    {
        [HttpGet, Route("request-log")]
        public IHttpActionResult GetLog([FromUri] int limit = 100)
        {
            if (limit < 1)  limit = 1;
            if (limit > 500) limit = 500;
            return Ok(new {
                Status  = "S",
                Message = "Request log",
                Data    = RequestLoggingHandler.GetRecentLog(limit)
            });
        }

        [HttpGet, Route("request-log/stats")]
        public IHttpActionResult GetStats()
        {
            return Ok(new {
                Status  = "S",
                Message = "Request statistics",
                Data    = RequestLoggingHandler.GetStats()
            });
        }
    }
}
