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
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    /// <summary>Picklist No validation RFC for GATELOT2 Inbound Process.</summary>
    public class ZVND_GATELOT2_PICKLIST_VAL_RFCController : BaseController
    {
        /// <summary>Validates picklist number for GATELOT2. Accepts IM_USER, IM_PLANT. Returns ET_DATA (ZTT_PICKLIST_NO).</summary>
        [HttpPost]
        [Route("api/ZVND_GATELOT2_PICKLIST_VAL_RFC")]
        public IHttpActionResult ZVND_GATELOT2_PICKLIST_VAL_RFC([FromBody] ZVND_GATELOT2_PICKLIST_VAL_RFCRequest request)
        {
            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination("SAP");
                IRfcFunction fn = dest.Repository.CreateFunction("ZVND_GATELOT2_PICKLIST_VAL_RFC");

                fn.SetValue("IM_USER",  request.IM_USER);
                fn.SetValue("IM_PLANT", request.IM_PLANT);

                fn.Invoke(dest);

                var exReturn = fn.GetStructure("EX_RETURN");
                var etData   = fn.GetTable("ET_DATA");

                var dataList = new List<object>();
                for (int i = 0; i < etData.Count; i++)
                {
                    etData.CurrentIndex = i;
                    dataList.Add(new { PICKLIST_NO = etData.GetValue("ZPICKLIST_NO").ToString() });
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