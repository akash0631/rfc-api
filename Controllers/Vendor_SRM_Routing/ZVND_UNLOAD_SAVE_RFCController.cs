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
    public class ZVND_UNLOAD_SAVE_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_UNLOAD_SAVE_RFC")]
        public async Task<HttpResponseMessage> ExecuteRFC([FromBody] ZVND_UNLOAD_SAVE_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request cannot be null" });
                }

                if (string.IsNullOrEmpty(request.IM_USER))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_USER is required" });
                }

                if (request.IM_PARMS == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_PARMS is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_UNLOAD_SAVE_RFC");

                myfun.SetValue("IM_USER", request.IM_USER);

                IRfcTable im_parms_table = myfun.GetTable("IM_PARMS");
                foreach (var parm in request.IM_PARMS)
                {
                    im_parms_table.Append();
                    foreach (var property in parm.GetType().GetProperties())
                    {
                        var value = property.GetValue(parm);
                        if (value != null)
                        {
                            im_parms_table.SetValue(property.Name.ToUpper(), value);
                        }
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = returnMessage });
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

    public class ZVND_UNLOAD_SAVE_RFCRequest
    {
        public string IM_USER { get; set; }
        public List<UnloadSaveItem> IM_PARMS { get; set; }
    }

    public class UnloadSaveItem
    {
        public string FIELD1 { get; set; }
        public string FIELD2 { get; set; }
        public string FIELD3 { get; set; }
        public string FIELD4 { get; set; }
        public string FIELD5 { get; set; }
    }
}
