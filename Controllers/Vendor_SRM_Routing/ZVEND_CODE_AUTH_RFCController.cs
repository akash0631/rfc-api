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
        public HttpResponseMessage ZVEND_CODE_AUTH_RFC(ZVEND_CODE_AUTH_RFC_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request cannot be null" });
                }

                if (string.IsNullOrEmpty(request.IM_PASS_ID))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_PASS_ID is required" });
                }

                if (string.IsNullOrEmpty(request.IM_PASSWORD))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_PASSWORD is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVEND_CODE_AUTH_RFC");

                myfun.SetValue("IM_PASS_ID", request.IM_PASS_ID);
                myfun.SetValue("IM_PASSWORD", request.IM_PASSWORD);

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE").ToString();
                string returnMessage = EX_RETURN.GetValue("MESSAGE").ToString();

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = returnMessage });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = returnType, Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZVEND_CODE_AUTH_RFC_Request
    {
        public string IM_PASS_ID { get; set; }
        public string IM_PASSWORD { get; set; }
    }
}