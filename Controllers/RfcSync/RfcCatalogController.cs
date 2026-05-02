using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_SRM_Routing_Application.Services;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// RFC Catalog API — browse and manage the Snowflake-driven RFC endpoint registry.
    ///
    /// GET  /api/catalog                  → list all active RFCs with params
    /// GET  /api/catalog/{rfcCode}        → single RFC details + params
    /// POST /api/catalog/refresh          → reload from GOLD.RFC_MASTER + GOLD.RFC_PARAM
    /// GET  /api/catalog/status           → registry health (count, last refresh, last error)
    /// </summary>
    [RoutePrefix("api/catalog")]
    public class RfcCatalogController : BaseController
    {
        private readonly EndpointRegistryService _registry = EndpointRegistryService.Instance;

        /// <summary>List all active RFC endpoints loaded from Snowflake RFC_MASTER.</summary>
        [HttpGet, Route("")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage GetAll(string dept = null, string search = null)
        {
            var all = _registry.GetAll();
            if (!string.IsNullOrWhiteSpace(dept))
                all = all.FindAll(e => string.Equals(e.Department, dept, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(search))
                all = all.FindAll(e =>
                    (e.RfcCode ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (e.DisplayName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            return Request.CreateResponse(HttpStatusCode.OK, new {
                Success      = true,
                Count        = all.Count,
                LastRefresh  = _registry.LastRefresh,
                Rfcs         = all
            });
        }

        /// <summary>Get full details for a single RFC including all parameters.</summary>
        [HttpGet, Route("{rfcCode}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Get(string rfcCode)
        {
            var ep = _registry.Get(rfcCode);
            if (ep == null)
                return Request.CreateResponse(HttpStatusCode.NotFound, new {
                    Success = false,
                    Error   = $"RFC '{rfcCode}' not found. Try POST /api/catalog/refresh first.",
                    TotalLoaded = _registry.Count
                });
            return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Rfc = ep });
        }

        /// <summary>
        /// Force reload of the RFC catalog from Snowflake GOLD.RFC_MASTER + GOLD.RFC_PARAM.
        /// Returns error detail if Snowflake is unreachable or query fails.
        /// </summary>
        [HttpPost, Route("refresh")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Refresh()
        {
            string error = _registry.Refresh();
            if (error != null)
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new {
                    Success = false,
                    Error   = error,
                    Message = "Catalog refresh failed. Check Snowflake connectivity and GOLD.RFC_MASTER table."
                });

            return Request.CreateResponse(HttpStatusCode.OK, new {
                Success     = true,
                Count       = _registry.Count,
                Message     = $"Catalog refreshed from Snowflake RFC_MASTER.",
                RefreshedAt = _registry.LastRefresh.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        /// <summary>Registry health — count, last refresh time, last error.</summary>
        [HttpGet, Route("status")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Status()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new {
                Success     = true,
                Count       = _registry.Count,
                LastRefresh = _registry.LastRefresh,
                LastError   = _registry.LastError,
                ServerTime  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }
    }
}
