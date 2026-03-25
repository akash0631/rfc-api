using System;
using System.Collections.Generic;
using System.Web.Http;
using SAP.Middleware.Connector;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    [RoutePrefix("api/ZpoModificationRfc")]
    public class ZPO_MODIFICATION_RFCController : ApiController
    {
        // ── Health ────────────────────────────────────────────────────────────
        [HttpGet, Route("Health")]
        public IHttpActionResult Health() =>
            Ok(new { Status = true, Message = "ZPO_MODIFICATION RFC controller is live." });

        // ── Execute ───────────────────────────────────────────────────────────
        [HttpPost, Route("Execute")]
        public IHttpActionResult Execute([FromBody] ZPO_MODIFICATION_Request req)
        {
            if (req == null)
                return BadRequest("Request body is required.");

            try
            {
                var rfcPar = BaseController.rfcConfigparameters();
                var dest = RfcDestinationManager.GetDestination(rfcPar);
                var repo = dest.Repository;
                var func = repo.CreateFunction("ZPO_MODIFICATION");

                // Import params
                if (!string.IsNullOrEmpty(req.IM_PO_NO))
                    func.SetValue("IM_PO_NO", req.IM_PO_NO);
                if (!string.IsNullOrEmpty(req.IM_PO_DEL_DATE))
                    func.SetValue("IM_PO_DEL_DATE", req.IM_PO_DEL_DATE);
                if (!string.IsNullOrEmpty(req.IM_DEL_CHG_DATE_LOW))
                    func.SetValue("IM_DEL_CHG_DATE_LOW", req.IM_DEL_CHG_DATE_LOW);
                if (!string.IsNullOrEmpty(req.IM_DEL_CHG_DATE_HIGH))
                    func.SetValue("IM_DEL_CHG_DATE_HIGH", req.IM_DEL_CHG_DATE_HIGH);

                func.Invoke(dest);

                // Export — EX_RETURN (BAPIRET2)
                var retStruct = func.GetStructure("EX_RETURN");
                var returnVal = new
                {
                    TYPE    = retStruct.GetString("TYPE"),
                    ID      = retStruct.GetString("ID"),
                    NUMBER  = retStruct.GetString("NUMBER"),
                    MESSAGE = retStruct.GetString("MESSAGE")
                };

                // Table — ET_PO_OUTPUT
                var table = func.GetTable("ET_PO_OUTPUT");
                var rows  = new List<ZPO_MODIFICATION_Row>();
                for (int i = 0; i < table.RowCount; i++)
                {
                    table.CurrentIndex = i;
                    rows.Add(new ZPO_MODIFICATION_Row
                    {
                        EBELN            = table.GetString("EBELN"),
                        ORIGNAL_DEL_DATE = table.GetString("ORIGNAL_DEL_DATE"),
                        CHNG_NO          = table.GetString("CHNG_NO"),
                        CURRENT_DEL_DATE = table.GetString("CURRENT_DEL_DATE"),
                        DEL_EXT_DATE     = table.GetString("DEL_EXT_DATE"),
                        DELAYED_BY       = table.GetString("DELAYED_BY"),
                        REASON           = table.GetString("REASON")
                    });
                }

                return Ok(new
                {
                    Status    = true,
                    EX_RETURN = returnVal,
                    ET_PO_OUTPUT = rows,
                    RowCount  = rows.Count
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
    public class ZPO_MODIFICATION_Request
    {
        /// <summary>Purchasing Document Number (EBELN / CHAR10)</summary>
        public string IM_PO_NO { get; set; }

        /// <summary>Item delivery date (EINDT / DATS8)</summary>
        public string IM_PO_DEL_DATE { get; set; }

        /// <summary>Posting Date — Low (ERDAT / DATS8) e.g. "20260101"</summary>
        public string IM_DEL_CHG_DATE_LOW { get; set; }

        /// <summary>Posting Date — High (ERDAT / DATS8) e.g. "20260319"</summary>
        public string IM_DEL_CHG_DATE_HIGH { get; set; }
    }

    public class ZPO_MODIFICATION_Row
    {
        public string EBELN            { get; set; }   // Purchasing Document Number
        public string ORIGNAL_DEL_DATE { get; set; }   // Original Delivery Date
        public string CHNG_NO          { get; set; }   // Change Number
        public string CURRENT_DEL_DATE { get; set; }   // Current Delivery Date
        public string DEL_EXT_DATE     { get; set; }   // Delivery Extension Date
        public string DELAYED_BY       { get; set; }   // Delayed By
        public string REASON           { get; set; }   // Reason
    }
}
