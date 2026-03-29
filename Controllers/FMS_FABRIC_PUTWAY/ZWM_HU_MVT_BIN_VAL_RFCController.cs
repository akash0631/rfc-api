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
        public HttpResponseMessage ZWM_HU_MVT_BIN_VAL_RFC([FromBody] ZWM_HU_MVT_BIN_VAL_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request body cannot be null"
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

                if (string.IsNullOrEmpty(request.IM_PLANT))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "IM_PLANT is required"
                    });
                }

                if (string.IsNullOrEmpty(request.IM_BIN))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "IM_BIN is required"
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
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
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = returnMessage
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = returnMessage
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }

    public class ZWM_HU_MVT_BIN_VAL_RFCRequest
    {
        public string IM_USER { get; set; }
        public string IM_PLANT { get; set; }
        public string IM_BIN { get; set; }
    }
}