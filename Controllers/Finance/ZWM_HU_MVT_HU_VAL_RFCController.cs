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
    public class ZWM_HU_MVT_HU_VAL_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZWM_HU_MVT_HU_VAL_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try {
                    if (request==null||request.IM_DATA==null||request.IM_DATA.Count==0)
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status=false, Message="IM_DATA required." });
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    IRfcFunction myfun = dest.Repository.CreateFunction("ZWM_HU_MVT_HU_VAL_RFC");
                    IRfcTable imData = myfun.GetTable("IM_DATA");
                    foreach (var row in request.IM_DATA) { imData.Append(); foreach (var kv in row) imData.SetValue(kv.Key, kv.Value??string.Empty); }
                    myfun.Invoke(dest);
                    string msgType = myfun.GetValue("MSG_TYPE")?.ToString()??string.Empty;
                    string message = myfun.GetValue("MESSAGE")?.ToString()??string.Empty;
                    if (msgType=="E") return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status=false, MsgType=msgType, Message=message });
                    return Request.CreateResponse(HttpStatusCode.OK, new { Status=true, MsgType=msgType, Message=message });
                } catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status=false, Message=ex.Message }); }
            });
        }
    }
    public class ZWM_HU_MVT_HU_VAL_RFCRequest { public List<Dictionary<string,string>> IM_DATA { get; set; } }
}