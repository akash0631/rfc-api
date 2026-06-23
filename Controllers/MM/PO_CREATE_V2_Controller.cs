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
    /// POST /api/PO_CREATE_V2
    ///
    /// Replacement for /api/ZMM_PO_CREATION_RFC. Drops the broken Z FM +
    /// SUBMIT report + memory ID architecture and calls BAPI_PO_CREATE1
    /// directly on S/4 PROD (.170 / Client 600).
    ///
    /// Same payload contract as the old endpoint:
    /// {
    ///   "IV_VENDOR":   "0000202633",
    ///   "IV_DOC_TYPE": "ZMNB",
    ///   "IT_ITEMS": [
    ///     {"MATERIAL":"<18-digit>","QTY":"1","NET_PRICE":"228.00",
    ///      "DEL_DATE":"20260910","PLANT":"DW01","STORAGE_LOC":"0001"}
    ///   ]
    /// }
    ///
    /// Response on success:
    ///   { "Status": true, "Message": "PO 4500001234 created", "PoNumber": "4500001234" }
    /// Response on error:
    ///   { "Status": false, "Message": "<concatenated SAP errors>", "PoNumber": "" }
    ///
    /// Hardcoded routing: ?env=prod is implicit; PROD only.
    /// </summary>
    public class PO_CREATE_V2_Controller : BaseController
    {
        private static readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private const int GateTimeoutSeconds = 60;

        [HttpPost]
        [Route("api/PO_CREATE_V2")]
        public async Task<HttpResponseMessage> Post([FromBody] PoCreateV2Request request)
        {
            if (request == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "Request body is required.", PoNumber = "" });
            if (string.IsNullOrWhiteSpace(request.IV_VENDOR))
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "IV_VENDOR is required.", PoNumber = "" });
            if (request.IT_ITEMS == null || request.IT_ITEMS.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "IT_ITEMS must contain at least one line item.", PoNumber = "" });

            bool entered = false;
            try
            {
                entered = await _gate.WaitAsync(TimeSpan.FromSeconds(GateTimeoutSeconds));
                if (!entered)
                    return Request.CreateResponse((HttpStatusCode)429, new { Status = false, Message = "PO creation queue is busy; please retry.", PoNumber = "" });

                return await Task.Run(() =>
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        IRfcFunction bapi = dest.Repository.CreateFunction("BAPI_PO_CREATE1");

                        string docType = string.IsNullOrWhiteSpace(request.IV_DOC_TYPE) ? "NB" : request.IV_DOC_TYPE;
                        string vendor = ZeroPad10(request.IV_VENDOR);
                        string headerPlant = (request.IT_ITEMS[0].PLANT ?? "").ToUpper();
                        string firstMatnr = ZeroPad18(request.IT_ITEMS[0].MATERIAL);
                        string ekgrp = LookupEkgrp(dest, firstMatnr, headerPlant);
                        if (string.IsNullOrWhiteSpace(ekgrp)) ekgrp = "110";

                        // POHEADER
                        IRfcStructure poHeader = bapi.GetStructure("POHEADER");
                        poHeader.SetValue("COMP_CODE", "1100");
                        poHeader.SetValue("DOC_TYPE", docType);
                        poHeader.SetValue("VENDOR", vendor);
                        poHeader.SetValue("PURCH_ORG", "1100");
                        poHeader.SetValue("PUR_GROUP", ekgrp);
                        poHeader.SetValue("CREAT_DATE", DateTime.Today.ToString("yyyyMMdd"));
                        poHeader.SetValue("DOC_DATE", DateTime.Today.ToString("yyyyMMdd"));
                        poHeader.SetValue("CURRENCY", "INR");

                        // POHEADERX
                        IRfcStructure poHeaderX = bapi.GetStructure("POHEADERX");
                        poHeaderX.SetValue("COMP_CODE", "X");
                        poHeaderX.SetValue("DOC_TYPE", "X");
                        poHeaderX.SetValue("VENDOR", "X");
                        poHeaderX.SetValue("PURCH_ORG", "X");
                        poHeaderX.SetValue("PUR_GROUP", "X");
                        poHeaderX.SetValue("CREAT_DATE", "X");
                        poHeaderX.SetValue("DOC_DATE", "X");
                        poHeaderX.SetValue("CURRENCY", "X");

                        // POITEM, POITEMX, POSCHEDULE, POSCHEDULEX
                        IRfcTable poItem = bapi.GetTable("POITEM");
                        IRfcTable poItemX = bapi.GetTable("POITEMX");
                        IRfcTable poSched = bapi.GetTable("POSCHEDULE");
                        IRfcTable poSchedX = bapi.GetTable("POSCHEDULEX");

                        int itemNo = 0;
                        foreach (var it in request.IT_ITEMS)
                        {
                            itemNo += 10;
                            string itemKey = itemNo.ToString("D5");

                            IRfcStructure itemRow = poItem.Metadata.LineType.CreateStructure();
                            itemRow.SetValue("PO_ITEM", itemKey);
                            itemRow.SetValue("MATERIAL", ZeroPad18(it.MATERIAL));
                            itemRow.SetValue("PLANT", (it.PLANT ?? "").ToUpper());
                            itemRow.SetValue("STGE_LOC", string.IsNullOrWhiteSpace(it.STORAGE_LOC) ? "0001" : it.STORAGE_LOC);
                            itemRow.SetValue("QUANTITY", it.QTY ?? "0");
                            itemRow.SetValue("PO_UNIT", "EA");
                            itemRow.SetValue("NET_PRICE", it.NET_PRICE ?? "0");
                            itemRow.SetValue("PRICE_UNIT", "1");
                            itemRow.SetValue("ITEM_CAT", "0");
                            poItem.Append(itemRow);

                            IRfcStructure itemX = poItemX.Metadata.LineType.CreateStructure();
                            itemX.SetValue("PO_ITEM", itemKey);
                            itemX.SetValue("PO_ITEMX", "X");
                            itemX.SetValue("MATERIAL", "X");
                            itemX.SetValue("PLANT", "X");
                            itemX.SetValue("STGE_LOC", "X");
                            itemX.SetValue("QUANTITY", "X");
                            itemX.SetValue("PO_UNIT", "X");
                            itemX.SetValue("NET_PRICE", "X");
                            itemX.SetValue("PRICE_UNIT", "X");
                            itemX.SetValue("ITEM_CAT", "X");
                            poItemX.Append(itemX);

                            IRfcStructure schedRow = poSched.Metadata.LineType.CreateStructure();
                            schedRow.SetValue("PO_ITEM", itemKey);
                            schedRow.SetValue("SCHED_LINE", "0001");
                            schedRow.SetValue("DELIVERY_DATE", it.DEL_DATE ?? "");
                            schedRow.SetValue("QUANTITY", it.QTY ?? "0");
                            poSched.Append(schedRow);

                            IRfcStructure schedX = poSchedX.Metadata.LineType.CreateStructure();
                            schedX.SetValue("PO_ITEM", itemKey);
                            schedX.SetValue("SCHED_LINE", "0001");
                            schedX.SetValue("PO_ITEMX", "X");
                            schedX.SetValue("SCHED_LINEX", "X");
                            schedX.SetValue("DELIVERY_DATE", "X");
                            schedX.SetValue("QUANTITY", "X");
                            poSchedX.Append(schedX);
                        }

                        bapi.Invoke(dest);

                        string poNumber = bapi.GetString("EXPPURCHASEORDER") ?? "";
                        IRfcTable returnTab = bapi.GetTable("RETURN");

                        var messages = new List<string>();
                        bool hasError = false;
                        foreach (IRfcStructure row in returnTab)
                        {
                            string type = row.GetString("TYPE") ?? "";
                            string msg = row.GetString("MESSAGE") ?? "";
                            if (type == "E" || type == "A") hasError = true;
                            messages.Add(type + ":" + msg);
                        }

                        bool success = !string.IsNullOrWhiteSpace(poNumber) && !hasError;
                        string joinedMsg = string.Join(" | ", messages);

                        if (success)
                        {
                            IRfcFunction commit = dest.Repository.CreateFunction("BAPI_TRANSACTION_COMMIT");
                            commit.SetValue("WAIT", "X");
                            commit.Invoke(dest);
                            return Request.CreateResponse(HttpStatusCode.OK,
                                new { Status = true, Message = "PO " + poNumber + " created. " + joinedMsg, PoNumber = poNumber });
                        }
                        else
                        {
                            IRfcFunction rollback = dest.Repository.CreateFunction("BAPI_TRANSACTION_ROLLBACK");
                            rollback.Invoke(dest);
                            return Request.CreateResponse(HttpStatusCode.BadRequest,
                                new { Status = false, Message = joinedMsg, PoNumber = poNumber });
                        }
                    }
                    catch (Exception ex)
                    {
                        return Request.CreateResponse(HttpStatusCode.InternalServerError,
                            new { Status = false, Message = ex.Message, PoNumber = "" });
                    }
                });
            }
            finally
            {
                if (entered) _gate.Release();
            }
        }

        /// <summary>
        /// Read MARC.EKGRP for the given material/plant via RFC_READ_TABLE.
        /// Returns empty string if not found.
        /// </summary>
        private static string LookupEkgrp(RfcDestination dest, string matnr, string werks)
        {
            try
            {
                IRfcFunction reader = dest.Repository.CreateFunction("RFC_READ_TABLE");
                reader.SetValue("QUERY_TABLE", "MARC");
                reader.SetValue("DELIMITER", "|");
                reader.SetValue("ROWCOUNT", 1);

                IRfcTable fields = reader.GetTable("FIELDS");
                IRfcStructure f1 = fields.Metadata.LineType.CreateStructure();
                f1.SetValue("FIELDNAME", "EKGRP");
                fields.Append(f1);

                IRfcTable options = reader.GetTable("OPTIONS");
                IRfcStructure o1 = options.Metadata.LineType.CreateStructure();
                o1.SetValue("TEXT", "MATNR = '" + matnr + "' AND WERKS = '" + werks + "'");
                options.Append(o1);

                reader.Invoke(dest);

                IRfcTable data = reader.GetTable("DATA");
                if (data.RowCount == 0) return "";
                data.CurrentIndex = 0;
                string wa = data.GetString("WA");
                return (wa ?? "").Trim();
            }
            catch
            {
                return "";
            }
        }

        private static string ZeroPad10(string s)
        {
            string digits = (s ?? "").Trim();
            if (string.IsNullOrEmpty(digits)) return "";
            return digits.PadLeft(10, '0');
        }

        private static string ZeroPad18(string s)
        {
            string raw = (s ?? "").Trim();
            if (string.IsNullOrEmpty(raw)) return "";
            // Already 18 digits → return as-is.
            if (raw.Length == 18 && long.TryParse(raw, out _)) return raw;
            // Numeric MATNR shorter than 18 → left-pad with zeros (CONVERSION_EXIT_MATN1_INPUT equivalent).
            if (long.TryParse(raw, out _)) return raw.PadLeft(18, '0');
            return raw;
        }
    }

    public class PoCreateV2Request
    {
        public string IV_VENDOR { get; set; }
        public string IV_DOC_TYPE { get; set; }
        public List<PoCreateV2Item> IT_ITEMS { get; set; }
    }

    public class PoCreateV2Item
    {
        public string MATERIAL { get; set; }
        public string QTY { get; set; }
        public string NET_PRICE { get; set; }
        public string DEL_DATE { get; set; }
        public string PLANT { get; set; }
        public string STORAGE_LOC { get; set; }
    }
}
