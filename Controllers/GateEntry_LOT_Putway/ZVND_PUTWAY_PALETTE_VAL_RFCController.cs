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
    /// <summary>Palette validation RFC for GATELOT2 Inbound Process.</summary>
    public class ZVND_PUTWAY_PALETTE_VAL_RFCController : BaseController
    {
        /// <summary>Validates palette for GATELOT2 inbound. Accepts IM_USER, IM_PLANT, IM_PICKLIST, IM_BIN, IM_PALL. Returns ET_BIN, ET_PALL, ET_BOX.</summary>
        [HttpPost]
        [Route("api/ZVND_GATELOT2_PALETTE_VAL_RFC")]
        public IHttpActionResult ZVND_GATELOT2_PALETTE_VAL_RFC([FromBody] ZVND_GATELOT2_PALETTE_VAL_RFCRequest request)
        {
            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination("SAP");
                IRfcFunction fn = dest.Repository.CreateFunction("ZVND_GATELOT2_PALETTE_VAL_RFC");

                fn.SetValue("IM_USER",     request.IM_USER);
                fn.SetValue("IM_PLANT",    request.IM_PLANT);
                fn.SetValue("IM_PICKLIST", request.IM_PICKLIST);
                fn.SetValue("IM_BIN",      request.IM_BIN);
                fn.SetValue("IM_PALL",     request.IM_PALL);

                fn.Invoke(dest);

                var exReturn = fn.GetStructure("EX_RETURN");
                var etBin    = fn.GetTable("ET_BIN");
                var etPall   = fn.GetTable("ET_PALL");
                var etBox    = fn.GetTable("ET_BOX");

                var binList  = new List<object>(); for (int i=0;i<etBin.Count;i++){etBin.CurrentIndex=i;binList.Add(new{BIN=etBin.GetValue("LGPLA").ToString()});}
                var pallList = new List<object>(); for (int i=0;i<etPall.Count;i++){etPall.CurrentIndex=i;pallList.Add(new{PALETTE=etPall.GetValue("ZZPALETTE").ToString()});}
                var boxList  = new List<object>(); for (int i=0;i<etBox.Count;i++){etBox.CurrentIndex=i;boxList.Add(new{HU=etBox.GetValue("ZEXT_HU").ToString()});}

                return Ok(new {
                    success = true,
                    data = new { ET_BIN=binList, ET_PALL=pallList, ET_BOX=boxList },
                    message = new { TYPE=exReturn.GetValue("TYPE").ToString(), MESSAGE=exReturn.GetValue("MESSAGE").ToString() }
                });
            }
            catch (RfcBaseException rfcEx) { return Content(HttpStatusCode.BadGateway, new{success=false,message=rfcEx.Message}); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
