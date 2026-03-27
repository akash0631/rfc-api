using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;
using Vendor_SRM_Routing_Application.Models.GateEntry_LOT_Putway;

namespace Vendor_SRM_Routing_Application.Controllers.GateEntry_LOT_Putway
{
    /// <summary>Save Data RFC for PUT01 Inbound Process.</summary>
    public class ZVND_PUT01_SAVE_DATA_RFCController : BaseController
    {
        /// <summary>Saves scanned HU data to SAP for PUT01. Accepts IM_USER + IT_DATA table (ZTT_PUT01_SAVE). Returns EX_RETURN.</summary>
        [HttpPost]
        [Route("api/ZVND_PUT01_SAVE_DATA_RFC")]
        public IHttpActionResult ZVND_PUT01_SAVE_DATA_RFC([FromBody] ZVND_PUT01_SAVE_DATA_RFCRequest request)
        {
            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination("SAP");
                IRfcFunction fn = dest.Repository.CreateFunction("ZVND_PUT01_SAVE_DATA_RFC");

                fn.SetValue("IM_USER", request.IM_USER);

                IRfcTable itData = fn.GetTable("IT_DATA");
                if (request.IT_DATA != null)
                {
                    foreach (var row in request.IT_DATA)
                    {
                        itData.Append();
                        itData.SetValue("ZEXT_HU",   row.HU);
                        itData.SetValue("ZZPALETTE",  row.PALETTE);
                        itData.SetValue("LGPLA",      row.BIN);
                        itData.SetValue("WERKS",      row.PLANT);
                        itData.SetValue("MENGE",      row.QTY);
                    }
                }

                fn.Invoke(dest);

                var exReturn = fn.GetStructure("EX_RETURN");
                return Ok(new {
                    success = exReturn.GetValue("TYPE").ToString() != "E",
                    data    = (object)null,
                    message = new { TYPE=exReturn.GetValue("TYPE").ToString(), MESSAGE=exReturn.GetValue("MESSAGE").ToString() }
                });
            }
            catch (RfcBaseException rfcEx) { return Content(HttpStatusCode.BadGateway, new{success=false,message=rfcEx.Message}); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}