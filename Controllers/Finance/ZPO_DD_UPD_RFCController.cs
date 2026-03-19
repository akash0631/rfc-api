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
    /// <summary>
    /// RFC: ZPO_DD_UPD_RFC
    /// Purpose: Update Delivery Date in SAP for one or more PO line items.
    /// IMPORT:  IM_DATA TYPE ZTT_PO_DD_IMP (Pass by Reference)
    ///          Table structure ZST_PO_DD_IMP:
    ///            EBELN      CHAR10  - Purchasing Document Number
    ///            PO_ITEM    NUMC5   - Item Number of Purchasing Document
    ///            MATNR      CHAR40  - Material Number
    ///            DELIV_DATE DATS8   - New Delivery Date (YYYYMMDD)
    /// EXPORT:  MSG_TYPE  CHAR1   - Message type (S=Success, E=Error, W=Warning)
    ///          MESSAGE   CHAR100 - Message text
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
                    if (request == null || request.IM_DATA == null || request.IM_DATA.Count == 0)
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = false,
                            Message = "IM_DATA table is required and must contain at least one row."
                        });
                    }

                    foreach (var row in request.IM_DATA)
                    {
                        if (string.IsNullOrWhiteSpace(row.EBELN))
                        {
                            return Request.CreateResponse(HttpStatusCode.BadRequest, new
                            {
                                Status  = false,
                                Message = "EBELN is required for every row."
                            });
                        }

                        if (string.IsNullOrWhiteSpace(row.PO_ITEM))
                        {
                            return Request.CreateResponse(HttpStatusCode.BadRequest, new
                            {
                                Status  = false,
                                Message = string.Format("PO_ITEM is required for EBELN {0}.", row.EBELN)
                            });
                        }

                        DateTime dummy;
                        if (string.IsNullOrWhiteSpace(row.DELIV_DATE) ||
                            !DateTime.TryParseExact(row.DELIV_DATE, "yyyyMMdd",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out dummy))
                        {
                            return Request.CreateResponse(HttpStatusCode.BadRequest, new
                            {
                                Status  = false,
                                Message = string.Format("DELIV_DATE for EBELN {0} must be YYYYMMDD.", row.EBELN)
                            });
                        }
                    }

                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination      dest   = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository       rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZPO_DD_UPD_RFC");

                    IRfcTable imData = myfun.GetTable("IM_DATA");
                    foreach (var row in request.IM_DATA)
                    {
                        imData.Append();
                        imData.SetValue("EBELN",      row.EBELN      ?? string.Empty);
                        imData.SetValue("PO_ITEM",    row.PO_ITEM    ?? string.Empty);
                        imData.SetValue("MATNR",      row.MATNR      ?? string.Empty);
                        imData.SetValue("DELIV_DATE", row.DELIV_DATE ?? string.Empty);
                    }

                    myfun.Invoke(dest);

                    string msgType = myfun.GetValue("MSG_TYPE")?.ToString() ?? string.Empty;
                    string message = myfun.GetValue("MESSAGE")?.ToString()  ?? string.Empty;

                    if (msgType == "E")
                        return Request.CreateResponse(HttpStatusCode.BadRequest,  new { Status = false, MsgType = msgType, Message = message });

                    return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, MsgType = msgType, Message = message });
                }
                catch (RfcCommunicationException ex) { return Request.CreateResponse(HttpStatusCode.ServiceUnavailable, new { Status = false, Message = "RFC comm error: " + ex.Message }); }
                catch (RfcLogonException ex)         { return Request.CreateResponse(HttpStatusCode.Unauthorized,        new { Status = false, Message = "SAP logon failed: " + ex.Message }); }
                catch (RfcAbapException ex)          { return Request.CreateResponse(HttpStatusCode.BadRequest,          new { Status = false, Message = "ABAP exception: " + ex.Message }); }
                catch (Exception ex)                 { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = false, Message = ex.Message }); }
            });
        }
    }

    public class ZPO_DD_UPD_RFCRequest
    {
        public List<ZST_PO_DD_IMP_Row> IM_DATA { get; set; }
    }

    public class ZST_PO_DD_IMP_Row
    {
        public string EBELN      { get; set; }
        public string PO_ITEM    { get; set; }
        public string MATNR      { get; set; }
        public string DELIV_DATE { get; set; }
    }
}
