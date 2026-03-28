using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor_SRM_Routing
{
    /// <summary>Article modification PO RFC — ZMM_ART_MOD_PO_RFC</summary>
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        /// <summary>
        /// Modifies article on PO.
        /// IMPORT table IM_INPUT: EBELN, MATNR, COLOR.
        /// EXPORT table IM_OUTPUT: EBELN, PO_STATUS, OLD_ART, NEW_ART, ART_STATUS.
        /// </summary>
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public IHttpActionResult ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination("SAP");
                IRfcFunction fn = dest.Repository.CreateFunction("ZMM_ART_MOD_PO_RFC");

                IRfcTable imInput = fn.GetTable("IM_INPUT");
                if (request != null && request.IM_INPUT != null)
                {
                    foreach (var row in request.IM_INPUT)
                    {
                        imInput.Append();
                        imInput.SetValue("EBELN", row.EBELN ?? "");
                        imInput.SetValue("MATNR", row.MATNR ?? "");
                        imInput.SetValue("COLOR", row.COLOR ?? "");
                    }
                }

                fn.Invoke(dest);

                IRfcTable imOutput = fn.GetTable("IM_OUTPUT");
                var outputList = new List<object>();
                for (int i = 0; i < imOutput.Count; i++)
                {
                    imOutput.CurrentIndex = i;
                    outputList.Add(new
                    {
                        EBELN      = imOutput.GetValue("EBELN")?.ToString(),
                        PO_STATUS  = imOutput.GetValue("PO_STATUS")?.ToString(),
                        OLD_ART    = imOutput.GetValue("OLD_ART")?.ToString(),
                        NEW_ART    = imOutput.GetValue("NEW_ART")?.ToString(),
                        ART_STATUS = imOutput.GetValue("ART_STATUS")?.ToString()
                    });
                }

                return Ok(new { success = true, data = outputList, message = "" });
            }
            catch (RfcBaseException rfcEx)
            {
                return Content(HttpStatusCode.BadGateway, new { success = false, message = rfcEx.Message });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    // ─── Request models ───────────────────────────────────────────────────
    /// <summary>Request body for ZMM_ART_MOD_PO_RFC</summary>
    public class ZMM_ART_MOD_PO_RFCRequest
    {
        /// <summary>Input table: PO + article data to modify</summary>
        public List<ZMM_ART_MOD_InputRow> IM_INPUT { get; set; }
    }

    /// <summary>Row for IM_INPUT table</summary>
    public class ZMM_ART_MOD_InputRow
    {
        /// <summary>Purchase Order number (EBELN)</summary>
        public string EBELN  { get; set; }
        /// <summary>Material/Article number (MATNR)</summary>
        public string MATNR  { get; set; }
        /// <summary>Color code</summary>
        public string COLOR  { get; set; }
    }
}