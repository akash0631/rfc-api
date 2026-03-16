using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    public class GENERATED_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/GENERATED_RFC")]
        public async Task<IHttpActionResult> ProcessGeneratedRFC([FromBody] GeneratedRFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request cannot be null");
                }

                var validationResult = ValidateRequest(request);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.ErrorMessage);
                }

                var rfcParameters = rfcConfigparameters();
                IRfcFunction rfcFunction = rfcParameters.Repository.CreateFunction("GENERATED_RFC");

                rfcFunction.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE ?? string.Empty);
                rfcFunction.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW ?? string.Empty);
                rfcFunction.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH ?? string.Empty);

                rfcFunction.Invoke(rfcParameters.Destination);

                IRfcTable itCreditTable = rfcFunction.GetTable("IT_CREDIT");
                var creditData = new List<ITCreditItem>();

                if (itCreditTable != null)
                {
                    for (int i = 0; i < itCreditTable.RowCount; i++)
                    {
                        itCreditTable.CurrentIndex = i;
                        var creditItem = new ITCreditItem
                        {
                            DOCUMENT_TYPE = itCreditTable.GetString("DOCUMENT_TYPE"),
                            COMPANY_CODE = itCreditTable.GetString("COMPANY_CODE"),
                            DOCUMENT_NUMBER = itCreditTable.GetString("DOCUMENT_NUMBER"),
                            FISCAL_YEAR = itCreditTable.GetString("FISCAL_YEAR"),
                            LINE_ITEM = itCreditTable.GetString("LINE_ITEM"),
                            POSTING_KEY = itCreditTable.GetString("POSTING_KEY"),
                            ACCOUNT_TYPE = itCreditTable.GetString("ACCOUNT_TYPE"),
                            SPECIAL_G_L_IND = itCreditTable.GetString("SPECIAL_G_L_IND"),
                            TRANSACT_TYPE = itCreditTable.GetString("TRANSACT_TYPE"),
                            DEBIT_CREDIT = itCreditTable.GetString("DEBIT_CREDIT"),
                            AMOUNT_IN_LC = itCreditTable.GetDecimal("AMOUNT_IN_LC"),
                            AMOUNT = itCreditTable.GetDecimal("AMOUNT"),
                            TEXT = itCreditTable.GetString("TEXT"),
                            VENDOR = itCreditTable.GetString("VENDOR"),
                            PAYMENT_AMT = itCreditTable.GetDecimal("PAYMENT_AMT"),
                            POSTING_DATE = itCreditTable.GetString("POSTING_DATE")
                        };
                        creditData.Add(creditItem);
                    }
                }

                var response = new GeneratedRFCResponse
                {
                    Status = "SUCCESS",
                    Message = "RFC executed successfully",
                    Data = new GeneratedRFCData
                    {
                        IT_CREDIT = creditData
                    }
                };

                return Ok(response);
            }
            catch (RfcCommunicationException ex)
            {
                return Content(HttpStatusCode.InternalServerError, new GeneratedRFCResponse
                {
                    Status = "ERROR",
                    Message = $"SAP Communication Error: {ex.Message}",
                    Data = null
                });
            }
            catch (RfcLogonException ex)
            {
                return Content(HttpStatusCode.Unauthorized, new GeneratedRFCResponse
                {
                    Status = "ERROR",
                    Message = $"SAP Logon Error: {ex.Message}",
                    Data = null
                });
            }
            catch (RfcAbapRuntimeException ex)
            {
                return Content(HttpStatusCode.BadRequest, new GeneratedRFCResponse
                {
                    Status = "ERROR",
                    Message = $"SAP Runtime Error: {ex.Message}",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new GeneratedRFCResponse
                {
                    Status = "ERROR",
                    Message = $"Unexpected error: {ex.Message}",
                    Data = null
                });
            }
        }

        private ValidationResult ValidateRequest(GeneratedRFCRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.I_COMPANY_CODE))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "I_COMPANY_CODE is required" };
            }

            if (request.I_COMPANY_CODE.Length > 4)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "I_COMPANY_CODE must not exceed 4 characters" };
            }

            if (!string.IsNullOrEmpty(request.I_POSTING_DATE_LOW) && !IsValidSAPDate(request.I_POSTING_DATE_LOW))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "I_POSTING_DATE_LOW must be in YYYYMMDD format" };
            }

            if (!string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH) && !IsValidSAPDate(request.I_POSTING_DATE_HIGH))
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "I_POSTING_DATE_HIGH must be in YYYYMMDD format" };
            }

            if (!string.IsNullOrEmpty(request.I_POSTING_DATE_LOW) && !string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
            {
                if (DateTime.TryParseExact(request.I_POSTING_DATE_LOW, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime lowDate) &&
                    DateTime.TryParseExact(request.I_POSTING_DATE_HIGH, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime highDate))
                {
                    if (lowDate > highDate)
                    {
                        return new ValidationResult { IsValid = false, ErrorMessage = "I_POSTING_DATE_LOW must be less than or equal to I_POSTING_DATE_HIGH" };
                    }
                }
            }

            return new ValidationResult { IsValid = true };
        }

        private bool IsValidSAPDate(string dateString)
        {
            return DateTime.TryParseExact(dateString, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out _);
        }

        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }
    }

    public class GeneratedRFCRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }

    public class GeneratedRFCResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public GeneratedRFCData Data { get; set; }
    }

    public class GeneratedRFCData
    {
        public List<ITCreditItem> IT_CREDIT { get; set; }
    }

    public class ITCreditItem
    {
        public string DOCUMENT_TYPE { get; set; }
        public string COMPANY_CODE { get; set; }
        public string DOCUMENT_NUMBER { get; set; }
        public string FISCAL_YEAR { get; set; }
        public string LINE_ITEM { get; set; }
        public string POSTING_KEY { get; set; }
        public string ACCOUNT_TYPE { get; set; }
        public string SPECIAL_G_L_IND { get; set; }
        public string TRANSACT_TYPE { get; set; }
        public string DEBIT_CREDIT { get; set; }
        public decimal AMOUNT_IN_LC { get; set; }
        public decimal AMOUNT { get; set; }
        public string TEXT { get; set; }
        public string VENDOR { get; set; }
        public decimal PAYMENT_AMT { get; set; }
        public string POSTING_DATE { get; set; }
    }
}