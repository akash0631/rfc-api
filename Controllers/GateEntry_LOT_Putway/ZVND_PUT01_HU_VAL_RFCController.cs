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
    /// <summary>HU validation RFC for PUT01 Inbound Process.</summary>
    public class ZVND_PUT01_HU_VAL_RFCController : BaseController
    {
        /// <summary>Validates HU for PUT01. Accepts IM_USER, IM_PLANT, IM_HU. Returns ET_DATA (ZTT_PUT01_HU).</summary>
        [HttpPost]
        [Route("api/ZVND_PUT01_HU_VAL_RFC")]
        public IHttpActionResult ZVND_PUT01_HU_VAL_RFC([FromBody] ZVND_PUT01_HU_VAL_RFCRequest request)
        {
            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination("SAP");
                IRfcFunction fn = dest.Repository.CreateFunction("ZVND_PUT01_HU_VAL_RFC");

                fn.SetValue("IM_USER",  request.IM_USER);
                fn.SetValue("IM_PLANT", request.IM_PLANT);
                fn.SetValue("IM_HU",    request.IM_HU);

                fn.Invoke(dest);

                var exReturn = fn.GetStructure("EX_RETURN");
                var etData   = fn.GetTable("ET_DATA");

                var dataList = new List<object>();
                for (int i = 0; i < etData.Count; i++)
                {
                    etData.CurrentIndex = i;
                    dataList.Add(new {
                        HU       = etData.GetValue("ZEXT_HU").ToString(),
                        PALETTE  = etData.GetValue("ZZPALETTE")?.ToString(),
                        PO_NO    = etData.GetValue("EBELN")?.ToString(),
                        INV_NO   = etData.GetValue("XBLNR")?.ToString(),
                        HU_QTY   = etData.GetValue("MENGE")?.ToString()
                    });
                }

                return Ok(new {
                    success = true,
                    data    = dataList,
                    message = new { TYPE=exReturn.GetValue("TYPE").ToString(), MESSAGE=exReturn.GetValue("MESSAGE").ToString() }
                });
            }
            catch (RfcBaseException rfcEx) { return Content(HttpStatusCode.BadGateway, new{success=false,message=rfcEx.Message}); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}