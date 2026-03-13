using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    /// <summary>
    /// API Controller for ZADVANCE_PAYMENT_RFC
    /// Technical Details: Fetches advance payment documents for a company code
    ///                    within a posting date range.
    ///
    /// IMPORT:
    ///   I_COMPANY_CODE      TYPE BUKRS   — Company Code
    ///   I_POSTING_DATE_LOW  TYPE BUDAT   — Posting Date From
    ///   I_POSTING_DATE_HIGH TYPE BUDAT   — Posting Date To
    ///
    /// EXPORT:
    ///   EX_RETURN           TYPE BAPIRET2
    ///
    /// TABLES (output):
    ///   IT_FINAL            TYPE ZADVANCE_TT  (structure: ZADVANCE_ST, 16 fields)
    ///     DOCUMENT_TYPE, COMPANY_CODE, DOCUMENT_NUMBER, FISCAL_YEAR,
    ///     LINE_ITEM, POSTING_KEY, ACCOUNT_TYPE, SPECIAL_G_L_IND,
    ///     TRANSACT_TYPE, DEBIT_CREDIT, AMOUNT_IN_LC, AMOUNT,
    ///     TEXT, VENDOR, PAYMENT_AMT, POSTING_DATE
    /// </summary>
    public class ZADVANCE_PAYMENT_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZADVANCE_PAYMENT_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request.I_COMPANY_CODE != null)
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = rfcrep.CreateFunction("ZADVANCE_PAYMENT_RFC");

                        // ── IMPORT parameters ──────────────────────────────
                        myfun.SetValue("I_COMPANY_CODE",      request.I_COMPANY_CODE);
                        myfun.SetValue("I_POSTING_DATE_LOW",  request.I_POSTING_DATE_LOW);
                        myfun.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                        myfun.Invoke(dest);

                        // ── EXPORT ─────────────────────────────────────────
                        IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                        string SAP_TYPE    = EX_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = EX_RETURN.GetValue("MESSAGE").ToString();

                        if (SAP_TYPE == "E")
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status  = false,
                                Message = SAP_Message
                            });
                        }

                        // ── TABLE: IT_FINAL (ZADVANCE_TT / ZADVANCE_ST) ───
                        IRfcTable itFinal = myfun.GetTable("IT_FINAL");
                        var meta = itFinal.Metadata.LineType;

                        var itFinalList = itFinal.AsEnumerable().Select(r =>
                        {
                            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < meta.FieldCount; i++)
                            {
                                var f = meta[i];
                                if (f.DataType == RfcDataType.STRUCTURE || f.DataType == RfcDataType.TABLE)
                                    continue;
                                try   { d[f.Name] = r.GetString(f.Name); }
                                catch { d[f.Name] = null; }
                            }
                            return d;
                        }).ToList();

                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status  = true,
                            Message = SAP_Message,
                            Data    = new
                            {
                                IT_FINAL = itFinalList
                            }
                        });
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status  = false,
                            Message = "Request Not Valid — I_COMPANY_CODE is required"
                        });
                    }
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

    public class ZADVANCE_PAYMENT_RFCRequest
    {
        /// <summary>TYPE: BUKRS — Company Code (required)</summary>
        public string I_COMPANY_CODE { get; set; }

        /// <summary>TYPE: BUDAT — Posting Date From (format: YYYYMMDD)</summary>
        public string I_POSTING_DATE_LOW { get; set; }

        /// <summary>TYPE: BUDAT — Posting Date To (format: YYYYMMDD)</summary>
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}
