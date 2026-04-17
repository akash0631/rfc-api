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
    public class ZWM_HU_STOCK_REV_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZWM_HU_STOCK_REV_RFC")]
        public async Task<HttpResponseMessage> ZWM_HU_STOCK_REV_RFC([FromBody] ZWM_HU_STOCK_REV_RFCRequest request)
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

                    // SAP RFC connection and function call
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZWM_HU_STOCK_REV_RFC");

                    // Set import parameters
                    myfun.SetValue("IV_WERKS", request.IV_WERKS);
                    myfun.SetValue("IV_HU", request.IV_HU);
                    myfun.SetValue("IV_LGTYP", request.IV_LGTYP);

                    // Invoke the function
                    myfun.Invoke(dest);

                    // Get return structure
                    IRfcTable EX_RETURN = myfun.GetTable("EX_RETURN");

                    // Check for errors in EX_RETURN
                    if (EX_RETURN.RowCount > 0)
                    {
                        foreach (IRfcStructure row in EX_RETURN)
                        {
                            string returnType = row.GetString("TYPE");
                            string returnMessage = row.GetString("MESSAGE");
                            
                            if (returnType == "E")
                            {
                                return Request.CreateResponse(HttpStatusCode.OK, new
                                {
                                    Status = "E",
                                    Message = returnMessage
                                });
                            }
                        }
                    }

                    // Success response
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "S",
                        Message = "HU stock reversal completed successfully"
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