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

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    public class ZWM_HU_MVT_SAVE_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZWM_HU_MVT_SAVE_RFC")]
        public async Task<IHttpActionResult> SaveHUMovement([FromBody] ZWM_HU_MVT_SAVE_RFC_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Request cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZWM_HU_MVT_SAVE_RFC");

                myfun.SetValue("IM_USER", request.IM_USER ?? string.Empty);
                myfun.SetValue("IM_PLANT", request.IM_PLANT ?? string.Empty);
                myfun.SetValue("IM_BIN", request.IM_BIN ?? string.Empty);
                myfun.SetValue("IM_HU", request.IM_HU ?? string.Empty);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Json(new { Status = "E", Message = returnMessage });
                }

                return Json(new { Status = "S", Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZWM_HU_MVT_SAVE_RFC_Request
    {
        public string IM_USER { get; set; }
        public string IM_PLANT { get; set; }
        public string IM_BIN { get; set; }
        public string IM_HU { get; set; }
    }
}