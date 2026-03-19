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
    /// RFC: ZPO_COST_UPD_RFC
    /// Purpose: Update PO Cost in SAP for one or more PO line items.
    /// IMPORT:  IM_DATA TYPE ZTT_PO_IMP (Pass by Reference)
    ///          Table structure ZST_PO_IMP:
    ///            EBELN   CHAR10  - Purchasing Document Number
    ///            MATNR   CHAR40  - Material Number
    ///            PO_ITEM NUMC5   - Item Number of Purchasing Document
    ///            COST    CHAR13  - Cost
    /// EXPORT:  MSG_TYPE CHAR1   - Message type (S=Success, E=Error, W=Warning)
    ///          MESSAGE  CHAR100 - Message text
    /// </summary>
    public class ZPO_COST_UPD_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZPO_COST_UPD_RFCRequest request)
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

                    IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                    // Populate IM_DATA table (ZTT_PO_IMP)
                    IRfcTable imData = myfun.GetTable("IM_DATA");
                    foreach (var row in request.IM_DATA)
                    {
                        imData.Append();
                        imData.SetValue("EBELN",   row.EBELN   ?? string.Empty);
                        imData.SetValue("MATNR",   row.MATNR   ?? string.Empty);
                        imData.SetValue("PO_ITEM", row.PO_ITEM ?? string.Empty);
                        imData.SetValue("COST",    row.COST    ?? string.Empty);
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
    public class ZPO_COST_UPD_RFCRequest
    {
        /// <summary>Table of PO cost update records (ZTT_PO_IMP)</summary>
        public List<ZST_PO_IMP_Row> IM_DATA { get; set; }
    }

    public class ZST_PO_IMP_Row
    {
        /// <summary>EBELN CHAR10 - Purchasing Document Number</summary>
        public string EBELN   { get; set; }

        /// <summary>MATNR CHAR40 - Material Number</summary>
        public string MATNR   { get; set; }

        /// <summary>PO_ITEM NUMC5 - Item Number of Purchasing Document</summary>
        public string PO_ITEM { get; set; }

        /// <summary>COST CHAR13 - New cost value</summary>
        public string COST    { get; set; }
    }
}
