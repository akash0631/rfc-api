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
        public async Task<IHttpActionResult> GetCreditData([FromBody] GeneratedRfcRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Request cannot be null",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Validate required parameters
                if (string.IsNullOrEmpty(request.I_COMPANY_CODE))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "I_COMPANY_CODE is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_LOW))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "I_POSTING_DATE_LOW is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "I_POSTING_DATE_HIGH is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Validate date format
                if (!IsValidDate(request.I_POSTING_DATE_LOW))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Invalid I_POSTING_DATE_LOW format",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (!IsValidDate(request.I_POSTING_DATE_HIGH))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Invalid I_POSTING_DATE_HIGH format",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Validate date range
                DateTime dateFrom = DateTime.ParseExact(request.I_POSTING_DATE_LOW, "yyyyMMdd", null);
                DateTime dateTo = DateTime.ParseExact(request.I_POSTING_DATE_HIGH, "yyyyMMdd", null);

                if (dateFrom > dateTo)
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "I_POSTING_DATE_LOW cannot be greater than I_POSTING_DATE_HIGH",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Get RFC configuration
                var rfcConfig = rfcConfigparameters();
                if (rfcConfig?.RfcDestination == null)
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "RFC configuration not available",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                IRfcFunction function = rfcConfig.RfcDestination.Repository.CreateFunction("GENERATED_RFC");
                if (function == null)
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "RFC function GENERATED_RFC not found",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Set import parameters
                function.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                function.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                function.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                // Execute RFC
                function.Invoke(rfcConfig.RfcDestination);

                // Get table data
                IRfcTable itCreditTable = function.GetTable("IT_CREDIT");
                var creditData = new List<object>();

                if (itCreditTable != null)
                {
                    foreach (IRfcStructure row in itCreditTable)
                    {
                        var creditItem = new
                        {
                            DOCUMENT_TYPE = GetSafeString(row, "DOCUMENT_TYPE"),
                            COMPANY_CODE = GetSafeString(row, "COMPANY_CODE"),
                            DOCUMENT_NUMBER = GetSafeString(row, "DOCUMENT_NUMBER"),
                            FISCAL_YEAR = GetSafeString(row, "FISCAL_YEAR"),
                            LINE_ITEM = GetSafeString(row, "LINE_ITEM"),
                            POSTING_KEY = GetSafeString(row, "POSTING_KEY"),
                            ACCOUNT_TYPE = GetSafeString(row, "ACCOUNT_TYPE"),
                            SPECIAL_G_L_IND = GetSafeString(row, "SPECIAL_G_L_IND"),
                            TRANSACT_TYPE = GetSafeString(row, "TRANSACT_TYPE"),
                            DEBIT_CREDIT = GetSafeString(row, "DEBIT_CREDIT"),
                            AMOUNT_IN_LC = GetSafeDecimal(row, "AMOUNT_IN_LC"),
                            AMOUNT = GetSafeDecimal(row, "AMOUNT"),
                            TEXT = GetSafeString(row, "TEXT"),
                            VENDOR = GetSafeString(row, "VENDOR"),
                            PAYMENT_AMT = GetSafeDecimal(row, "PAYMENT_AMT"),
                            POSTING_DATE = GetSafeString(row, "POSTING_DATE")
                        };
                        creditData.Add(creditItem);
                    }
                }

                return Ok(new
                {
                    Status = "Success",
                    Message = "Data retrieved successfully",
                    Data = new { IT_CREDIT = creditData }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "Error",
                    Message = $"RFC Communication Error: {ex.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                });
            }
            catch (RfcLogonException ex)
            {
                return Ok(new
                {
                    Status = "Error",
                    Message = $"RFC Logon Error: {ex.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                });
            }
            catch (RfcAbapRuntimeException ex)
            {
                return Ok(new
                {
                    Status = "Error",
                    Message = $"RFC ABAP Runtime Error: {ex.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "Error",
                    Message = $"Unexpected error: {ex.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                });
            }
        }

        private bool IsValidDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString) || dateString.Length != 8)
                return false;

            return DateTime.TryParseExact(dateString, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out _);
        }

        private string GetSafeString(IRfcStructure structure, string fieldName)
        {
            try
            {
                return structure.GetString(fieldName)?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private decimal GetSafeDecimal(IRfcStructure structure, string fieldName)
        {
            try
            {
                return structure.GetDecimal(fieldName);
            }
            catch
            {
                return 0;
            }
        }
    }

    public class GeneratedRfcRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}