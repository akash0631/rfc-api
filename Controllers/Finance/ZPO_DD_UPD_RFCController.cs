using System;
using System.Web.Http;
using SAP.Middleware.Connector;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    [RoutePrefix("api/ZpoDdUpdRfc")]
    public class ZPO_DD_UPD_RFCController : ApiController
    {
        // ── Health ────────────────────────────────────────────────────────────
        [HttpGet, Route("Health")]
        public IHttpActionResult Health() =>
            Ok(new { Status = true, Message = "ZPO_DD_UPD_RFC controller is live." });

        // ── Execute ───────────────────────────────────────────────────────────
        [HttpPost, Route("Execute")]
        public IHttpActionResult Execute([FromBody] ZPO_DD_UPD_Request req)
        {
            if (req == null)
                return BadRequest("Request body is required.");

            try
            {
                var dest = RfcDestinationManager.GetDestination(SAPConfig.DestinationName);
                var repo = dest.Repository;
                var func = repo.CreateFunction("ZPO_DD_UPD_RFC");

                // Import params
                if (!string.IsNullOrEmpty(req.PO_NO))
                    func.SetValue("PO_NO", req.PO_NO);
                if (!string.IsNullOrEmpty(req.DELV_DATE))
                    func.SetValue("DELV_DATE", req.DELV_DATE);

                func.Invoke(dest);

                // Export params
                var msgType = func.GetString("MSG_TYPE");
                var message = func.GetString("MESSAGE");

                return Ok(new
                {
                    Status   = true,
                    MSG_TYPE = msgType,
                    MESSAGE  = message
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new { Status = false, Message = ex.Message, Key = ex.Key });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    // ── Models ──────────────────────────────────────────────────────────────
    public class ZPO_DD_UPD_Request
    {
        /// <summary>Purchasing Document Number (EBELN / CHAR10)</summary>
        public string PO_NO { get; set; }

        /// <summary>Delivery Date (CHAR10) — format YYYYMMDD or DD.MM.YYYY</summary>
        public string DELV_DATE { get; set; }
    }
}
