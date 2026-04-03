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
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_EXP_UPLOAD_RFC")]
        public async Task<HttpResponseMessage> ZFI_EXP_UPLOAD_RFC([FromBody] ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request body cannot be null",
                        Data = new { EX_RETURN = new List<object>() }
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                if (request.IM_INPUT != null)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = imInputTable.Metadata.LineType.CreateStructure();
                        var properties = inputItem.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(inputItem);
                            if (value != null)
                            {
                                inputRow.SetValue(prop.Name, value);
                            }
                        }
                        imInputTable.Append(inputRow);
                    }
                }

                if (request.IM_OUTPUT != null)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = imOutputTable.Metadata.LineType.CreateStructure();
                        var properties = outputItem.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(outputItem);
                            if (value != null)
                            {
                                outputRow.SetValue(prop.Name, value);
                            }
                        }
                        imOutputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);

                // EX_RETURN is a STRUCTURE (BAPIRET2), not a TABLE
                IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                var returnDict = new Dictionary<string, object>();
                for (int i = 0; i < exReturn.Metadata.FieldCount; i++)
                {
                    var field = exReturn.Metadata[i];
                    if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                    {
                        returnDict[field.Name] = exReturn.GetString(field.Name);
                    }
                }
                var returnData = new List<Dictionary<string, object>> { returnDict };

                var hasError = returnData.Any(item => item.ContainsKey("TYPE") && item["TYPE"]?.ToString() == "E");
                if (hasError)
                {
                    var errorMessage = returnData.Where(item => item.ContainsKey("TYPE") && item["TYPE"]?.ToString() == "E")
                                                .Select(item => item.ContainsKey("MESSAGE") ? item["MESSAGE"]?.ToString() : "Unknown error")
                                                .FirstOrDefault() ?? "Error occurred during RFC execution";

                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = errorMessage,
                        Data = new { EX_RETURN = returnData }
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Finance expense upload processed successfully",
                    Data = new { EX_RETURN = returnData }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { EX_RETURN = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { EX_RETURN = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { EX_RETURN = new List<object>() }
                });
            }
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
    }

    /// <summary>
    /// ZFI_EXP_UPLOAD_RFC — Finance Expense Upload
    /// System: QUALITY (192.168.144.179, Client 600)
    /// 
    /// POST /api/ZFI_EXP_UPLOAD_RFC
    /// 
    /// MANDATORY FIELDS (minimum required):
    ///   COMPANY_CODE  — Company code (e.g. "V201")
    ///   VENDOR_CODE   — Vendor number with leading zeros (e.g. "0000400001")  
    ///   INVOICE_DATE  — YYYYMMDD format (e.g. "20260403")
    ///   POSTING_DATE  — YYYYMMDD format (e.g. "20260403")
    ///   GL_CODE       — GL Account with leading zeros (e.g. "0040000000")
    ///   AMOUNT        — Amount with decimals (e.g. "1000.00")
    ///   COST_CENTER   — Cost center with leading zeros (e.g. "1000000000")
    /// 
    /// OPTIONAL FIELDS:
    ///   HEADER_TEXT, VENDOR_LINE_TEXT, GL_LINE_TEXT — Text fields
    ///   TAX_CODE — Tax code (e.g. "V0")
    ///   WH_TAX_CODE — Withholding tax code
    ///   BUSINESS_AREA, PROFIT_CENTER, ASSIGNMENT_NO
    ///   REFRENCE_NUMBER — Reference document number
    ///
    /// To check mandatory fields: SE37 → ZFI_EXP_UPLOAD_RFC → Source code
    ///   Look for IF...IS INITIAL checks — those are mandatory.
    ///
    /// EX_RETURN: BAPIRET2 structure
    ///   TYPE = S (success) or E (error)
    ///   MESSAGE = status message from SAP
    /// </summary>
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