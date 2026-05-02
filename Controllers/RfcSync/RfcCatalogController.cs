using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_SRM_Routing_Application.Services;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// RFC Catalog API — reads from Snowflake GOLD.RFC_MASTER.
    /// Every active RFC in the catalog is automatically callable via /api/execute/{rfcCode}.
    /// No code deployment needed to add new RFCs — just insert a row in RFC_MASTER.
    /// </summary>
    [RoutePrefix("api/catalog")]
    public class RfcCatalogController : BaseController
    {
        private readonly EndpointRegistryService _registry = EndpointRegistryService.Instance;

        /// <summary>
        /// List all active RFCs from Snowflake RFC_MASTER catalog.
        /// Each RFC listed here is immediately callable via POST /api/execute/{rfcCode}.
        /// </summary>
        [HttpGet, Route("")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage GetAll(string department = null, string module = null)
        {
            try
            {
                var all = _registry.GetAll();
                if (!string.IsNullOrWhiteSpace(department))
                    all = all.FindAll(e => string.Equals(e.Department, department, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(module))
                    all = all.FindAll(e => string.Equals(e.SapModule, module, StringComparison.OrdinalIgnoreCase));

                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success    = true,
                    Total      = all.Count,
                    LastRefresh = _registry.LastRefresh.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Source     = "Snowflake GOLD.RFC_MASTER",
                    Rfcs       = all
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// Get details of a specific RFC including all input/output parameters.
        /// </summary>
        [HttpGet, Route("{rfcCode}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage GetOne(string rfcCode)
        {
            try
            {
                var ep = _registry.Get(rfcCode);
                if (ep == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new { Success = false, Error = "RFC '" + rfcCode + "' not found in catalog." });

                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success    = true,
                    Rfc        = ep,
                    ExecuteUrl = "/api/execute/" + rfcCode,
                    SyncUrl    = "/api/execute/" + rfcCode + "/sync"
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// Force-refresh the RFC catalog from Snowflake RFC_MASTER.
        /// Call after adding/editing rows in RFC_MASTER — no restart needed.
        /// </summary>
        [HttpPost, Route("refresh")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Refresh()
        {
            try
            {
                _registry.Refresh();
                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success     = true,
                    Message     = "Catalog refreshed from Snowflake RFC_MASTER.",
                    TotalRfcs   = _registry.GetAll().Count,
                    RefreshedAt = _registry.LastRefresh.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }
    }
}
