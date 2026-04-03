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
        public async Task<HttpResponseMessage> ZVND_UNLOAD_SAVE_RFC([FromBody] ZVND_UNLOAD_SAVE_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request body cannot be null" });
                }

                if (string.IsNullOrEmpty(request.IM_USER))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_USER is required" });
                }

                if (request.IM_PARMS == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_PARMS is required" });
                }

                await Task.Run(() =>
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_UNLOAD_SAVE_RFC");

                    myfun.SetValue("IM_USER", request.IM_USER);

                    IRfcTable parmsTable = myfun.GetTable("IM_PARMS");
                    foreach (var parm in request.IM_PARMS)
                    {
                        parmsTable.Append();
                        parmsTable.CurrentRow.SetValue("CLIENT", parm.CLIENT ?? "");
                        parmsTable.CurrentRow.SetValue("UNLOAD_ID", parm.UNLOAD_ID ?? "");
                        parmsTable.CurrentRow.SetValue("VENDOR_CODE", parm.VENDOR_CODE ?? "");
                        parmsTable.CurrentRow.SetValue("PLANT", parm.PLANT ?? "");
                        parmsTable.CurrentRow.SetValue("MATERIAL", parm.MATERIAL ?? "");
                        parmsTable.CurrentRow.SetValue("BATCH", parm.BATCH ?? "");
                        parmsTable.CurrentRow.SetValue("QUANTITY", parm.QUANTITY);
                        parmsTable.CurrentRow.SetValue("UOM", parm.UOM ?? "");
                        parmsTable.CurrentRow.SetValue("STORAGE_LOCATION", parm.STORAGE_LOCATION ?? "");
                        parmsTable.CurrentRow.SetValue("CREATED_BY", parm.CREATED_BY ?? "");
                        parmsTable.CurrentRow.SetValue("CREATED_DATE", parm.CREATED_DATE ?? "");
                        parmsTable.CurrentRow.SetValue("CREATED_TIME", parm.CREATED_TIME ?? "");
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
                });

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "S", Message = "Operation completed successfully" });
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

    public class ZVND_UNLOAD_SAVE_Request
    {
        public string IM_USER { get; set; }
        public List<ZTT_UNLOAD_SAVE_Item> IM_PARMS { get; set; }
    }

    public class ZTT_UNLOAD_SAVE_Item
    {
        public string CLIENT { get; set; }
        public string UNLOAD_ID { get; set; }
        public string VENDOR_CODE { get; set; }
        public string PLANT { get; set; }
        public string MATERIAL { get; set; }
        public string BATCH { get; set; }
        public decimal QUANTITY { get; set; }
        public string UOM { get; set; }
        public string STORAGE_LOCATION { get; set; }
        public string CREATED_BY { get; set; }
        public string CREATED_DATE { get; set; }
        public string CREATED_TIME { get; set; }
    }
}