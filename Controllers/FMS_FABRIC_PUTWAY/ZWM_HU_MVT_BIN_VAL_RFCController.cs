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
        public async Task<HttpResponseMessage> ZWM_HU_MVT_BIN_VAL_RFC(ZWM_HU_MVT_BIN_VAL_RFC_Request request)
        {
            try
            {
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
                    var errorResponse = new
                    {
                        Status = "E",
                        Message = returnMessage
                    };
                    return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
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
                return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
            }
          
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
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
