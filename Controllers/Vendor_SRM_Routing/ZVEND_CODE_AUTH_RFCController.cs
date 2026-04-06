using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor_SRM_Routing
{
    [Route("api/ZVEND_CODE_AUTH_RFC")]
    public class ZVEND_CODE_AUTH_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVEND_CODE_AUTH_RFC")]
        public async Task<HttpResponseMessage> ExecuteZVEND_CODE_AUTH_RFC([FromBody] ZVEND_CODE_AUTH_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Validate required input parameters
                    if (request == null)
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = "Request cannot be null"
                        });
                    }

                    if (string.IsNullOrEmpty(request.IM_USER_ID))
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = "IM_USER_ID is required"
                        });
                    }

                    if (string.IsNullOrEmpty(request.IM_PASSWORD))
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = "IM_PASSWORD is required"
                        });
                    }

                    // SAP RFC connector pattern
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZVEND_CODE_AUTH_RFC");

                    // Set import parameters
                    myfun.SetValue("IM_USER_ID", request.IM_USER_ID);
                    myfun.SetValue("IM_PASSWORD", request.IM_PASSWORD);

                    // Invoke the RFC
                    myfun.Invoke(dest);

                    // Get EX_RETURN structure
                    IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                    // Check return status
                    string returnType = EX_RETURN.GetString("TYPE");
                    string returnMessage = EX_RETURN.GetString("MESSAGE");

                    if (returnType == "E")
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = returnMessage
                        });
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "S",
                        Message = !string.IsNullOrEmpty(returnMessage) ? returnMessage : "Vendor code authentication successful"
                    });
                }
                catch (RfcAbapException ex)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = ex.Message
                    });
                }
                catch (RfcCommunicationException ex)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = ex.Message
                    });
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = ex.Message
                    });
                }
            });
        }
    }

    public class ZVEND_CODE_AUTH_RFCRequest
    {
        public string IM_USER_ID { get; set; }
        public string IM_PASSWORD { get; set; }
    }
}
