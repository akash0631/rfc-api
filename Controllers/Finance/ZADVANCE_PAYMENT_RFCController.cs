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
    public class ZAdvancePaymentRfcController : BaseController
    {
        [HttpPost]
        [Route("api/ZAdvancePaymentRfc/Execute")]
        public async Task<IHttpActionResult> ExecuteRfc([FromBody] ZAdvancePaymentRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request cannot be null");
                }

                // Validate required parameters
                if (string.IsNullOrEmpty(request.I_COMPANY_CODE))
                {
                    return BadRequest("I_COMPANY_CODE is required");
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_LOW))
                {
                    return BadRequest("I_POSTING_DATE_LOW is required");
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
                {
                    return BadRequest("I_POSTING_DATE_HIGH is required");
                }

                // Validate date format and range
                DateTime postingDateLow, postingDateHigh;
                if (!DateTime.TryParseExact(request.I_POSTING_DATE_LOW, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out postingDateLow))
                {
                    return BadRequest("I_POSTING_DATE_LOW must be in YYYYMMDD format");
                }

                if (!DateTime.TryParseExact(request.I_POSTING_DATE_HIGH, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out postingDateHigh))
                {
                    return BadRequest("I_POSTING_DATE_HIGH must be in YYYYMMDD format");
                }

                if (postingDateLow > postingDateHigh)
                {
                    return BadRequest("I_POSTING_DATE_LOW cannot be greater than I_POSTING_DATE_HIGH");
                }

                var rfcParameters = new Dictionary<string, object>
                {
                    { "I_COMPANY_CODE", request.I_COMPANY_CODE },
                    { "I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW },
                    { "I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH }
                };

                var result = await Task.Run(() => rfcConfigparameters("ZADVANCE_PAYMENT_RFC", rfcParameters));

                if (result != null && result.ContainsKey("IT_CREDIT"))
                {
                    var itCreditData = result["IT_CREDIT"] as IRfcTable;
                    var creditList = new List<AdvancePaymentCredit>();

                    if (itCreditData != null)
                    {
                        foreach (IRfcStructure row in itCreditData)
                        {
                            var creditItem = new AdvancePaymentCredit
                            {
                                DOCUMENT_TYPE = row.GetString("DOCUMENT_TYPE"),
                                COMPANY_CODE = row.GetString("COMPANY_CODE"),
                                DOCUMENT_NUMBER = row.GetString("DOCUMENT_NUMBER"),
                                FISCAL_YEAR = row.GetString("FISCAL_YEAR"),
                                LINE_ITEM = row.GetString("LINE_ITEM"),
                                POSTING_KEY = row.GetString("POSTING_KEY"),
                                ACCOUNT_TYPE = row.GetString("ACCOUNT_TYPE"),
                                SPECIAL_G_L_IND = row.GetString("SPECIAL_G_L_IND"),
                                TRANSACT_TYPE = row.GetString("TRANSACT_TYPE"),
                                DEBIT_CREDIT = row.GetString("DEBIT_CREDIT"),
                                AMOUNT_IN_LC = row.GetDecimal("AMOUNT_IN_LC"),
                                AMOUNT = row.GetDecimal("AMOUNT"),
                                TEXT = row.GetString("TEXT"),
                                VENDOR = row.GetString("VENDOR"),
                                PAYMENT_AMT = row.GetDecimal("PAYMENT_AMT"),
                                POSTING_DATE = row.GetString("POSTING_DATE")
                            };
                            creditList.Add(creditItem);
                        }
                    }

                    var response = new ZAdvancePaymentResponse
                    {
                        Status = "Success",
                        Message = "Advance payment data retrieved successfully",
                        Data = new ZAdvancePaymentData
                        {
                            IT_CREDIT = creditList
                        }
                    };

                    return Ok(response);
                }
                else
                {
                    return Ok(new ZAdvancePaymentResponse
                    {
                        Status = "Success",
                        Message = "No advance payment data found for the specified criteria",
                        Data = new ZAdvancePaymentData
                        {
                            IT_CREDIT = new List<AdvancePaymentCredit>()
                        }
                    });
                }
            }
            catch (RfcCommunicationException rfcEx)
            {
                return Ok(new ZAdvancePaymentResponse
                {
                    Status = "Error",
                    Message = $"SAP RFC Communication Error: {rfcEx.Message}",
                    Data = null
                });
            }
            catch (RfcLogonException logonEx)
            {
                return Ok(new ZAdvancePaymentResponse
                {
                    Status = "Error",
                    Message = $"SAP RFC Logon Error: {logonEx.Message}",
                    Data = null
                });
            }
            catch (RfcAbapException abapEx)
            {
                return Ok(new ZAdvancePaymentResponse
                {
                    Status = "Error",
                    Message = $"SAP ABAP Error: {abapEx.Key} - {abapEx.Message}",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                return Ok(new ZAdvancePaymentResponse
                {
                    Status = "Error",
                    Message = $"Unexpected error: {ex.Message}",
                    Data = null
                });
            }
        }

        [HttpGet]
        [Route("api/ZAdvancePaymentRfc/Health")]
        public IHttpActionResult HealthCheck()
        {
            return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow, Controller = "ZAdvancePaymentRfc" });
        }
    }

    public class ZAdvancePaymentRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }

    public class ZAdvancePaymentResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public ZAdvancePaymentData Data { get; set; }
    }

    public class ZAdvancePaymentData
    {
        public List<AdvancePaymentCredit> IT_CREDIT { get; set; }
    }

    public class AdvancePaymentCredit
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