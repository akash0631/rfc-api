using SAP.Middleware.Connector;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    /// <summary>
    /// RFC: ZPO_DD_UPD_RFC
    /// Purpose: Update Delivery Date on a Purchase Order in SAP.
    /// IMPORT:  PO_NO     TYPE EBELN  - Purchase Order Number (Pass by Reference)
    ///          DELV_DATE TYPE CHAR10  - New Delivery Date (Pass by Reference)
    /// EXPORT:  MSG_TYPE  TYPE CHAR1  - Message type: S=Success, E=Error, W=Warning
    ///          MESSAGE   TYPE CHAR100 - Message text
    /// </summary>
    public class ZPO_DD_UPD_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZPO_DD_UPD_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request == null)
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = false,
                            Message = "Request body is required."
                        });
                    }

                    if (string.IsNullOrWhiteSpace(request.PO_NO))
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = false,
                            Message = "PO_NO is required."
                        });
                    }

                    if (string.IsNullOrWhiteSpace(request.DELV_DATE))
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = false,
                            Message = "DELV_DATE is required."
                        });
                    }

                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination      dest   = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository       rfcrep = dest.Repository;

                    IRfcFunction myfun = rfcrep.CreateFunction("ZPO_DD_UPD_RFC");

                    myfun.SetValue("PO_NO",     request.PO_NO.Trim());
                    myfun.SetValue("DELV_DATE", request.DELV_DATE.Trim());

                    myfun.Invoke(dest);

                    string msgType = myfun.GetValue("MSG_TYPE")?.ToString() ?? string.Empty;
                    string message = myfun.GetValue("MESSAGE")?.ToString()  ?? string.Empty;

                    if (msgType == "E")
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status   = false,
                            MsgType  = msgType,
                            Message  = message
                        });
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status   = true,
                        MsgType  = msgType,
                        Message  = message
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

    public class ZPO_DD_UPD_RFCRequest
    {
        /// <summary>EBELN - Purchase Order Number</summary>
        public string PO_NO     { get; set; }

        /// <summary>CHAR10 - New Delivery Date (format: YYYYMMDD)</summary>
        public string DELV_DATE { get; set; }
    }
}
