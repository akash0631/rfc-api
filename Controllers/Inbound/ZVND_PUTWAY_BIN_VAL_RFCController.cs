using SAP.Middleware.Connector;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    /// <summary>
    /// RFC: ZVND_PUTWAY_BIN_VAL_RFC
    /// Purpose: BIN validation RFC for Inbound Putway Process.
    /// IMPORT: IM_USER (WWWOBJID), IM_PLANT (WERKS_D), IM_BIN (LGPLA)
    /// EXPORT: EX_RETURN (BAPIRET2)
    /// </summary>
    public class ZVND_PUTWAY_BIN_VAL_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZVND_PUTWAY_BIN_VAL_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;

                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_PUTWAY_BIN_VAL_RFC");

                    myfun.SetValue("IM_USER",  request.IM_USER);
                    myfun.SetValue("IM_PLANT", request.IM_PLANT);
                    myfun.SetValue("IM_BIN",   request.IM_BIN);

                    myfun.Invoke(dest);

                    IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                    string sapType    = exReturn.GetValue("TYPE").ToString();
                    string sapMessage = exReturn.GetValue("MESSAGE").ToString();

                    if (sapType == "E")
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = false,
                            Message = sapMessage
                        });
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status  = true,
                        Message = sapMessage
                    });
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status  = false,
                        Message = ex.Message
                    });
                }
            });
        }
    }

    public class ZVND_PUTWAY_BIN_VAL_RFCRequest
    {
        public string IM_USER  { get; set; }   // WWWOBJID - logged-in user
        public string IM_PLANT { get; set; }   // WERKS_D  - plant/DC (auto-fetched)
        public string IM_BIN   { get; set; }   // LGPLA    - scanned bin location
    }
}
