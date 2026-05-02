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
    [Route("api/ZVND_UNLOAD_SAVE_RFC")]
    public class ZVND_UNLOAD_SAVE_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> PostAsync([FromBody] ZVND_UNLOAD_SAVE_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request == null)
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status = "E",
                            Message = "Request cannot be null"
                        });
                    }

                    if (string.IsNullOrEmpty(request.IM_USER))
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status = "E",
                            Message = "IM_USER is required"
                        });
                    }

                    if (request.IM_PARMS == null || request.IM_PARMS.Count == 0)
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status = "E",
                            Message = "IM_PARMS is required"
                        });
                    }

                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_UNLOAD_SAVE_RFC");

                    myfun.SetValue("IM_USER", request.IM_USER);

                    IRfcTable imParmsTable = myfun.GetTable("IM_PARMS");
                    foreach (var parmItem in request.IM_PARMS)
                    {
                        imParmsTable.Append();
                        if (!string.IsNullOrEmpty(parmItem.PARAM_NAME))
                            imParmsTable.SetValue("PARAM_NAME", parmItem.PARAM_NAME);
                        if (!string.IsNullOrEmpty(parmItem.PARAM_VALUE))
                            imParmsTable.SetValue("PARAM_VALUE", parmItem.PARAM_VALUE);
                        if (!string.IsNullOrEmpty(parmItem.PARAM_TYPE))
                            imParmsTable.SetValue("PARAM_TYPE", parmItem.PARAM_TYPE);
                    }

                    myfun.Invoke(dest);

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
                        Message = string.IsNullOrEmpty(returnMessage) ? "Operation completed successfully" : returnMessage
                    });
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
            });
        }
    }

    public class ZVND_UNLOAD_SAVE_RFCRequest
    {
        public string IM_USER { get; set; }
        public List<ZTT_UNLOAD_SAVE_Item> IM_PARMS { get; set; }
    }

    public class ZTT_UNLOAD_SAVE_Item
    {
        public string PARAM_NAME { get; set; }
        public string PARAM_VALUE { get; set; }
        public string PARAM_TYPE { get; set; }
    }
}
