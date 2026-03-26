using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    [Route("api/ZADVANCE_PAYMENT_RFC")]
    public class ZADVANCE_PAYMENT_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZADVANCE_PAYMENT_RFC")]
        public HttpResponseMessage ProcessAdvancePayment(ZADVANCE_PAYMENT_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZADVANCE_PAYMENT_RFC");
                
                myfun.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                
                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");
                
                if (returnType == "E")
                {
                    var errorResponse = new
                    {
                        Status = "E",
                        Message = returnMessage
                    };
                    return Request.CreateResponse(HttpStatusCode.BadRequest, errorResponse);
                }
                
                var successResponse = new
                {
                    Status = returnType,
                    Message = returnMessage
                };
                
                return Request.CreateResponse(HttpStatusCode.OK, successResponse);
            }
            catch (RfcAbapException ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
            catch (CommunicationException ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
        }
    }
    
    public class ZADVANCE_PAYMENT_RFCRequest
    {
        public string I_COMPANY_CODE { get; set; }
    }
}