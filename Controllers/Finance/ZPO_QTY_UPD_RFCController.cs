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
    /// RFC: ZPO_QTY_UPD_RFC
    /// Purpose: Update PO Quantity in SAP for one or more PO line items.
    /// IMPORT:  IM_DATA TYPE ZTT_PO_IMP_QTY (Pass by Reference)
    ///          Table structure ZST_PO_IMP_QTY:
    ///            EBELN   CHAR10  - Purchasing Document Number
    ///            MATNR   CHAR40  - Material Number
    ///            PO_ITEM NUMC5   - Item Number of Purchasing Document
    ///            QTY     CHAR13  - Quantity
    /// EXPORT:  MSG_TYPE CHAR1   - Message type (S=Success, E=Error, W=Warning)
    ///          MESSAGE  CHAR100 - Message text
    /// </summary>
    public class ZPO_QTY_UPD_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZPO_QTY_UPD_RFCRequest request)
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

                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination      dest   = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository       rfcrep = dest.Repository;

                    IRfcFunction myfun = rfcrep.CreateFunction("ZPO_QTY_UPD_RFC");

                    // Populate IM_DATA table (ZTT_PO_IMP_QTY)
                    IRfcTable imData = myfun.GetTable("IM_DATA");
                    foreach (var row in request.IM_DATA)
                    {
                        imData.Append();
                        imData.SetValue("EBELN",   row.EBELN   ?? string.Empty);
                        imData.SetValue("MATNR",   row.MATNR   ?? string.Empty);
                        imData.SetValue("PO_ITEM", row.PO_ITEM ?? string.Empty);
                        imData.SetValue("QTY",     row.QTY     ?? string.Empty);
                    }

                    myfun.Invoke(dest);

                    // Read export parameters
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

    // ── Request model ──────────────────────────────────────────────────────────
    public class ZPO_QTY_UPD_RFCRequest
    {
        /// <summary>Table of PO quantity update records (ZTT_PO_IMP_QTY)</summary>
        public List<ZST_PO_IMP_QTY_Row> IM_DATA { get; set; }
    }

    public class ZST_PO_IMP_QTY_Row
    {
        /// <summary>EBELN CHAR10 - Purchasing Document Number</summary>
        public string EBELN   { get; set; }

        /// <summary>MATNR CHAR40 - Material Number</summary>
        public string MATNR   { get; set; }

        /// <summary>PO_ITEM NUMC5 - Item Number of Purchasing Document</summary>
        public string PO_ITEM { get; set; }

        /// <summary>QTY CHAR13 - New quantity value</summary>
        public string QTY     { get; set; }
    }
}
