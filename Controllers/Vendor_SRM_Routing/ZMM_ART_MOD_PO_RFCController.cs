using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.Vendor_SRM_Routing;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor_SRM_Routing
{
    /// <summary>Article modification PO RFC — ZMM_ART_MOD_PO_RFC</summary>
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        /// <summary>
        /// Modify article on PO in SAP.
        /// IMPORT: IM_INPUT table (EBELN, MATNR, COLOR).
        /// EXPORT: IM_OUTPUT table (EBELN, PO_STATUS, OLD_ART, NEW_ART, ART_STATUS).
        /// </summary>
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public IHttpActionResult ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination("SAP");
                IRfcFunction fn = dest.Repository.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Populate IM_INPUT table
                IRfcTable imInput = fn.GetTable("IM_INPUT");
                if (request.IM_INPUT != null)
                {
                    foreach (var row in request.IM_INPUT)
                    {
                        imInput.Append();
                        imInput.SetValue("EBELN",  row.EBELN  ?? "");
                        imInput.SetValue("MATNR",  row.MATNR  ?? "");
                        imInput.SetValue("COLOR",  row.COLOR  ?? "");
                    }
                }

                fn.Invoke(dest);

                // Read IM_OUTPUT table
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
}