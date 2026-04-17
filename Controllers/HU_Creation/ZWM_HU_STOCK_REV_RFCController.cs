using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor_SRM_Routing
{
    public class ZWM_HU_STOCK_REV_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZWM_HU_STOCK_REV_RFC")]
        public async Task<HttpResponseMessage> ExecuteZWM_HU_STOCK_REV_RFC(ZWM_HU_STOCK_REV_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Validate required parameters
                    if (request == null)
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = "Request cannot be null"
                        });
                    }

                    if (string.IsNullOrEmpty(request.IV_WERKS))
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = "IV_WERKS is required"
                        });
                    }

                    if (string.IsNullOrEmpty(request.IV_HU))
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = "IV_HU is required"
                        });
                    }

                    if (string.IsNullOrEmpty(request.IV_LGTYP))
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = "IV_LGTYP is required"
                        });
                    }

                    // SAP RFC connection
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZWM_HU_STOCK_REV_RFC");

                    // Set input parameters
                    myfun.SetValue("IV_WERKS", request.IV_WERKS);
                    myfun.SetValue("IV_HU", request.IV_HU);
                    myfun.SetValue("IV_LGTYP", request.IV_LGTYP);

                    // Execute RFC
                    myfun.Invoke(dest);

                    // Get return structure
                    IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

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
                        Message = string.IsNullOrEmpty(returnMessage) ? "HU Stock Reversal executed successfully" : returnMessage
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

    public class ZWM_HU_STOCK_REV_RFCRequest
    {
        public string IV_WERKS { get; set; }
        public string IV_HU { get; set; }
        public string IV_LGTYP { get; set; }
    }
}