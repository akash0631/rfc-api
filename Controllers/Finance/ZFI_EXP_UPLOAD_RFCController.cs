using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using System.Linq;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    /// <summary>
    /// ZFI_EXP_UPLOAD_RFC — Finance Expense Document Upload
    /// System: QUALITY (192.168.144.179, Client 600)
    /// 
    /// POST /api/ZFI_EXP_UPLOAD_RFC
    /// 
    /// MANDATORY FIELDS (from ABAP source analysis):
    ///   COMPANY_CODE    — Company code (e.g. "V201") [BUKRS CHAR 4]
    ///   VENDOR_CODE     — Vendor number with leading zeros (e.g. "0000400001") [LIFNR CHAR 10]
    ///   INVOICE_DATE    — Document date YYYYMMDD (e.g. "20260403") [BLDAT DATS 8]
    ///   POSTING_DATE    — Posting date YYYYMMDD (e.g. "20260403") [BUDAT DATS 8]
    ///   GL_CODE         — GL Account with leading zeros (e.g. "0040000000") [SAKNR CHAR 10]
    ///   AMOUNT          — Amount with decimals (e.g. "1000.00") [WRBTR CURR 23.2]
    ///   TAX_CODE        — Tax code (e.g. "V0") — checked: "Tax code is blank for GL" [MWSKZ CHAR 2]
    ///   COST_CENTER     — Cost center (e.g. "1000000000") [KOSTL CHAR 10]
    ///   REFRENCE_NUMBER — Reference doc number, checked for duplicates against BKPF/BSEG [XBLNR CHAR 16]
    /// 
    /// OPTIONAL FIELDS:
    ///   HEADER_TEXT      — Document header text [BKTXT CHAR 25]
    ///   VENDOR_LINE_TEXT — Vendor line item text [SGTXT CHAR 50]
    ///   GL_LINE_TEXT     — GL line item text [SGTXT CHAR 50]
    ///   WH_TAX_CODE      — Withholding tax code, auto-filled from LFBW master if blank [WT_WITHCD CHAR 2]
    ///   BUSINESS_AREA    — Business area, hardcoded to "1000" in program [GSBER CHAR 4]
    ///   PROFIT_CENTER    — Derived from COST_CENTER first 4 chars if blank [PRCTR CHAR 10]
    ///   ASSIGNMENT_NO    — Assignment number [DZUONR CHAR 18]
    ///   HSN_SAC          — HSN or SAC code [J_1IG_HSN_SAC CHAR 16]
    ///
    /// VALIDATIONS (from FORM VALIDATE_DATA):
    ///   1. GL Account + Vendor combo checked against ZFI02 table
    ///   2. Duplicate REFRENCE_NUMBER checked against BKPF/BSEG for current fiscal year
    ///   3. WH_TAX_CODE validated against LFBW vendor master
    ///
    /// EX_RETURN: BAPIRET2 STRUCTURE (not TABLE)
    ///   TYPE = S (success) → "Document posted successfully"
    ///   TYPE = E (error) → error message from SAP
    ///
    /// IM_OUTPUT: TABLE of ZFI_OUTPUT_STRUC — contains per-row results with STATUS, TYPE, MESSAGE
    /// </summary>
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_EXP_UPLOAD_RFC")]
        public async Task<HttpResponseMessage> ZFI_EXP_UPLOAD_RFC([FromBody] ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                if (request == null || request.IM_INPUT == null || request.IM_INPUT.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "IM_INPUT is required and must contain at least one row",
                        Data = new { EX_RETURN = new object(), IM_OUTPUT = new List<object>() }
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                // IM_INPUT is a TABLE parameter — must use GetTable + Append
                IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                foreach (var item in request.IM_INPUT)
                {
                    imInputTable.Append();
                    // Set each field explicitly using TrySetValue to avoid crashes on unknown fields
                    TrySet(imInputTable, "COMPANY_CODE", item.COMPANY_CODE);
                    TrySet(imInputTable, "VENDOR_CODE", item.VENDOR_CODE);
                    TrySet(imInputTable, "INVOICE_DATE", item.INVOICE_DATE);
                    TrySet(imInputTable, "POSTING_DATE", item.POSTING_DATE);
                    TrySet(imInputTable, "HEADER_TEXT", item.HEADER_TEXT);
                    TrySet(imInputTable, "WH_TAX_CODE", item.WH_TAX_CODE);
                    TrySet(imInputTable, "REFRENCE_NUMBER", item.REFRENCE_NUMBER);
                    TrySet(imInputTable, "VENDOR_LINE_TEXT", item.VENDOR_LINE_TEXT);
                    TrySet(imInputTable, "GL_CODE", item.GL_CODE);
                    TrySet(imInputTable, "AMOUNT", item.AMOUNT);
                    TrySet(imInputTable, "TAX_CODE", item.TAX_CODE);
                    TrySet(imInputTable, "COST_CENTER", item.COST_CENTER);
                    TrySet(imInputTable, "BUSINESS_AREA", item.BUSINESS_AREA);
                    TrySet(imInputTable, "PROFIT_CENTER", item.PROFIT_CENTER);
                    TrySet(imInputTable, "ASSIGNMENT_NO", item.ASSIGNMENT_NO);
                    TrySet(imInputTable, "GL_LINE_TEXT", item.GL_LINE_TEXT);
                    TrySet(imInputTable, "HSN_SAC", item.HSN_SAC);
                }

                myfun.Invoke(dest);

                // EX_RETURN is a STRUCTURE (BAPIRET2), NOT a TABLE
                IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                var returnDict = new Dictionary<string, string>();
                for (int i = 0; i < exReturn.Metadata.FieldCount; i++)
                {
                    var field = exReturn.Metadata[i];
                    if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                    {
                        returnDict[field.Name] = exReturn.GetString(field.Name);
                    }
                }

                // IM_OUTPUT is a TABLE — read response rows
                IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                var outputRows = new List<Dictionary<string, object>>();
                foreach (IRfcStructure row in imOutputTable)
                {
                    var outputRow = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            outputRow[field.Name] = row.GetString(field.Name);
                        }
                    }
                    outputRows.Add(outputRow);
                }

                string retType = returnDict.ContainsKey("TYPE") ? returnDict["TYPE"] : "";
                string retMsg = returnDict.ContainsKey("MESSAGE") ? returnDict["MESSAGE"] : "";

                if (retType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = retMsg,
                        Data = new { EX_RETURN = returnDict, IM_OUTPUT = outputRows }
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = retMsg,
                    Data = new { EX_RETURN = returnDict, IM_OUTPUT = outputRows }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = "SAP ABAP Error: " + ex.Message,
                    Data = new { EX_RETURN = new object(), IM_OUTPUT = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = "SAP Communication Error: " + ex.Message,
                    Data = new { EX_RETURN = new object(), IM_OUTPUT = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { EX_RETURN = new object(), IM_OUTPUT = new List<object>() }
                });
            }
        }

        private void TrySet(IRfcTable table, string fieldName, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            try { table.SetValue(fieldName, value); } catch { }
        }
    }

    public class ZFI_EXP_UPLOAD_RFCRequest
    {
        public List<ZFI_INPUT_STRUC> IM_INPUT { get; set; }
        public List<ZFI_OUTPUT_STRUC> IM_OUTPUT { get; set; }
    }

    public class ZFI_INPUT_STRUC
    {
        public string COMPANY_CODE { get; set; }
        public string VENDOR_CODE { get; set; }
        public string INVOICE_DATE { get; set; }
        public string POSTING_DATE { get; set; }
        public string HEADER_TEXT { get; set; }
        public string WH_TAX_CODE { get; set; }
        public string REFRENCE_NUMBER { get; set; }
        public string VENDOR_LINE_TEXT { get; set; }
        public string GL_CODE { get; set; }
        public string AMOUNT { get; set; }
        public string TAX_CODE { get; set; }
        public string COST_CENTER { get; set; }
        public string BUSINESS_AREA { get; set; }
        public string PROFIT_CENTER { get; set; }
        public string ASSIGNMENT_NO { get; set; }
        public string GL_LINE_TEXT { get; set; }
        public string HSN_SAC { get; set; }
    }

    public class ZFI_OUTPUT_STRUC
    {
        public string STATUS { get; set; }
        public string TYPE { get; set; }
        public string MESSAGE { get; set; }
        public string ROW_ID { get; set; }
        public string COMPANY_CODE { get; set; }
        public string INVOICE_DATE { get; set; }
        public string POSTING_DATE { get; set; }
        public string VENDOR_CODE { get; set; }
        public string HEADER_TEXT { get; set; }
        public string WH_TAX_CODE { get; set; }
        public string REFRENCE_NUMBER { get; set; }
        public string VENDOR_LINE_TEXT { get; set; }
        public string GL_CODE { get; set; }
        public string AMOUNT { get; set; }
        public string TAX_CODE { get; set; }
        public string COST_CENTER { get; set; }
        public string BUSINESS_AREA { get; set; }
        public string PROFIT_CENTER { get; set; }
        public string ASSIGNMENT_NO { get; set; }
        public string GL_LINE_TEXT { get; set; }
        public string HSN_SAC { get; set; }
    }
}
