using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.FabricPutway
{
    public class ZWM_HU_MVT_BIN_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZWM_HU_MVT_BIN_VAL_RFC")]
        public IHttpActionResult ExecuteZWM_HU_MVT_BIN_VAL_RFC([FromBody] ZWM_HU_MVT_BIN_VAL_RFC_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Request body cannot be null" });
                }

                if (string.IsNullOrEmpty(request.IM_USER))
                {
                    return Json(new { Status = "E", Message = "IM_USER is required" });
                }

                if (string.IsNullOrEmpty(request.IM_PLANT))
                {
                    return Json(new { Status = "E", Message = "IM_PLANT is required" });
                }

                if (string.IsNullOrEmpty(request.IM_BIN))
                {
                    return Json(new { Status = "E", Message = "IM_BIN is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZWM_HU_MVT_BIN_VAL_RFC");

                myfun.SetValue("IM_USER", request.IM_USER);
                myfun.SetValue("IM_PLANT", request.IM_PLANT);
                myfun.SetValue("IM_BIN", request.IM_BIN);

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
           
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZWM_HU_MVT_BIN_VAL_RFC_Request
    {
        public string IM_USER { get; set; }
        public string IM_PLANT { get; set; }
        public string IM_BIN { get; set; }
    }
}
