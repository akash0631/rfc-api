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
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVEND_CODE_AUTH_RFC");
                
                myfun.SetValue("IM_USER_ID", request.IM_USER_ID);
                myfun.SetValue("IM_PASSWORD", request.IM_PASSWORD);
                
                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                string status = EX_RETURN.GetValue("TYPE").ToString();
                string message = EX_RETURN.GetValue("MESSAGE").ToString();
                
                if (status == "E")
                {
                    var errorResponse = new
                    {
                        Status = "E",
                        Message = message
                    };
                    return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
                }
                
                var successResponse = new
                {
                    Status = status,
                    Message = message
                };
                
                return Request.CreateResponse(HttpStatusCode.OK, successResponse);
            }
            catch (RfcAbapException ex)
            {
                var response = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (RfcCommunicationException ex)
            {
                var response = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception ex)
            {
                var response = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
        }
    }

    public class ZVEND_CODE_AUTH_RFC_Request
    {
        public string IM_USER_ID { get; set; }
        public string IM_PASSWORD { get; set; }
    }
}