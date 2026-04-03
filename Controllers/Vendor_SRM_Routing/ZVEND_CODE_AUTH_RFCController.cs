using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor
{
    public class ZVEND_CODE_AUTH_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVEND_CODE_AUTH_RFC")]
        public async Task<IHttpActionResult> AuthenticateVendor([FromBody] ZVEND_CODE_AUTH_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Request body is null" });
                }

                if (string.IsNullOrEmpty(request.IM_USER_ID))
                {
                    return Json(new { Status = "E", Message = "IM_USER_ID is required" });
                }

                if (string.IsNullOrEmpty(request.IM_PASSWORD))
                {
                    return Json(new { Status = "E", Message = "IM_PASSWORD is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVEND_CODE_AUTH_RFC");

                myfun.SetValue("IM_USER_ID", request.IM_USER_ID);
                myfun.SetValue("IM_PASSWORD", request.IM_PASSWORD);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE")?.ToString();
                string returnMessage = EX_RETURN.GetValue("MESSAGE")?.ToString();

                if (returnType == "E")
                {
                    return Json(new { Status = "E", Message = returnMessage });
                }

                return Json(new { Status = returnType ?? "S", Message = returnMessage ?? "Success" });
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

    public class ZVEND_CODE_AUTH_RFCRequest
    {
        public string IM_USER_ID { get; set; }
        public string IM_PASSWORD { get; set; }
    }
}