using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.MM
{
    /// <summary>
    /// RFC: ZMM_PO_CREATION_RFC
    /// Purpose: Create SAP Purchase Order (header + line items) via existing FM
    ///          (FM internally SUBMITs report ZMM_POCREATE using SAP memory IDs
    ///          Z_PO_UPLOAD / Z_PO_TAX / Z_IT_FINAL -- concurrent calls are
    ///          serialized via a process-wide semaphore to prevent races).
    ///
    /// IMPORT:  IV_VENDOR    (LIFNR)  - vendor account (required)
    ///          IV_DOC_TYPE  (BSART)  - purchasing doc type, defaults to 'NB'
    /// TABLE:   IT_ITEMS     (ZPO_ITEM_STRUCTURE_TT) - line items
    ///                       Components: MATERIAL (MATNR), QTY (BSTMG),
    ///                       NET_PRICE (BPREI), DEL_DATE (EINDT, YYYYMMDD),
    ///                       PLANT (EWERK), STORAGE_LOC (LGORT_D)
    /// EXPORT:  EX_RETURN    (BAPIRET2) - TYPE 'S'/'E', MESSAGE,
    ///                       PARAMETER carries the new PO number on success.
    ///
    /// SAP Target: PRODUCTION (192.168.144.170 / Client 600 / PRD)
    /// </summary>
    public class ZMM_PO_CREATION_RFCController : BaseController
    {
        private static readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private const int GateTimeoutSeconds = 60;

        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZMM_PO_CREATION_RFCRequest request)
        {
            if (request == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "Request body is required." });

            if (string.IsNullOrWhiteSpace(request.IV_VENDOR))
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "IV_VENDOR is required." });

            if (request.IT_ITEMS == null || request.IT_ITEMS.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "IT_ITEMS must contain at least one line item." });

            bool entered = false;
            try
            {
                entered = await _gate.WaitAsync(TimeSpan.FromSeconds(GateTimeoutSeconds));
                if (!entered)
                    return Request.CreateResponse((HttpStatusCode)429, new { Status = false, Message = "PO creation queue is busy; please retry." });

                return await Task.Run(() =>
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction(); // PRODUCTION (.170 / Client 600 / PRD)
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        IRfcFunction myfun = dest.Repository.CreateFunction("ZMM_PO_CREATION_RFC");

                        myfun.SetValue("IV_VENDOR",   request.IV_VENDOR);
                        myfun.SetValue("IV_DOC_TYPE", string.IsNullOrWhiteSpace(request.IV_DOC_TYPE) ? "NB" : request.IV_DOC_TYPE);

                        IRfcTable itItems = myfun.GetTable("IT_ITEMS");
                        foreach (var row in request.IT_ITEMS)
                        {
                            itItems.Append();
                            itItems.SetValue("MATERIAL",    row.MATERIAL);
                            itItems.SetValue("QTY",         row.QTY);
                            itItems.SetValue("NET_PRICE",   row.NET_PRICE);
                            itItems.SetValue("DEL_DATE",    row.DEL_DATE);
                            itItems.SetValue("PLANT",       row.PLANT);
                            itItems.SetValue("STORAGE_LOC", row.STORAGE_LOC);
                        }

                        myfun.Invoke(dest);

                        IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                        string sapType    = exReturn.GetValue("TYPE")?.ToString() ?? "";
                        string sapMessage = exReturn.GetValue("MESSAGE")?.ToString() ?? "";
                        string poNumber   = exReturn.GetValue("PARAMETER")?.ToString() ?? "";

                        if (sapType == "E")
                            return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = sapMessage, PoNumber = poNumber });

                        return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Message = sapMessage, PoNumber = poNumber });
                    }
                    catch (Exception ex)
                    {
                        return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = false, Message = ex.Message });
                    }
                });
            }
            finally
            {
                if (entered) _gate.Release();
            }
        }
    }

    public class ZMM_PO_CREATION_RFCRequest
    {
        public string IV_VENDOR   { get; set; }
        public string IV_DOC_TYPE { get; set; }
        public List<ZMM_PO_CREATION_ItemRow> IT_ITEMS { get; set; }
    }

    public class ZMM_PO_CREATION_ItemRow
    {
        public string MATERIAL    { get; set; }
        public string QTY         { get; set; }
        public string NET_PRICE   { get; set; }
        public string DEL_DATE    { get; set; }
        public string PLANT       { get; set; }
        public string STORAGE_LOC { get; set; }
    }
}
