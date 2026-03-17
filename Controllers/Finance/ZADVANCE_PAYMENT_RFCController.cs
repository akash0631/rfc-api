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
        public async Task<IHttpActionResult> ExecuteRfc(ZAdvancePaymentRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request cannot be null");
                }

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

                var rfcResult = await ExecuteSapRfc(request);
                
                if (rfcResult.Status == "SUCCESS")
                {
                    return Ok(rfcResult);
                }
                else
                {
                    return Content(HttpStatusCode.InternalServerError, rfcResult);
                }
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    Status = "ERROR",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
        }

        private async Task<dynamic> ExecuteSapRfc(ZAdvancePaymentRequest request)
        {
            try
            {
                var parameters = rfcConfigparameters();
                IRfcFunction function = parameters.Repository.CreateFunction("ZADVANCE_PAYMENT_RFC");

                function.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                function.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                function.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                function.Invoke(parameters.Destination);

                IRfcTable itCreditTable = function.GetTable("IT_CREDIT");
                List<dynamic> creditData = new List<dynamic>();

                for (int i = 0; i < itCreditTable.RowCount; i++)
                {
                    itCreditTable.CurrentIndex = i;
                    
                    var creditItem = new
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

                return new
                {
                    Status = "SUCCESS",
                    Message = "RFC executed successfully",
                    Data = new
                    {
                        IT_CREDIT = creditData
                    }
                };
            }
            catch (RfcCommunicationException ex)
            {
                return new
                {
                    Status = "ERROR",
                    Message = $"RFC Communication Error: {ex.Message}",
                    Data = (object)null
                };
            }
            catch (RfcLogonException ex)
            {
                return new
                {
                    Status = "ERROR",
                    Message = $"RFC Logon Error: {ex.Message}",
                    Data = (object)null
                };
            }
            catch (RfcAbapRuntimeException ex)
            {
                return new
                {
                    Status = "ERROR",
                    Message = $"ABAP Runtime Error: {ex.Message}",
                    Data = (object)null
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Status = "ERROR",
                    Message = $"Unexpected error: {ex.Message}",
                    Data = (object)null
                };
            }
        }

        [HttpGet]
        public IHttpActionResult GetRfcMetadata()
        {
            try
            {
                var metadata = new
                {
                    RfcName = "ZADVANCE_PAYMENT_RFC",
                    Description = "SAP RFC controller for advance payment processing",
                    ImportParameters = new[]
                    {
                        new { Name = "I_COMPANY_CODE", Type = "String", Required = true },
                        new { Name = "I_POSTING_DATE_LOW", Type = "String", Required = true },
                        new { Name = "I_POSTING_DATE_HIGH", Type = "String", Required = true }
                    },
                    ExportTables = new[]
                    {
                        new
                        {
                            Name = "IT_CREDIT",
                            Fields = new[]
                            {
                                "DOCUMENT_TYPE", "COMPANY_CODE", "DOCUMENT_NUMBER", "FISCAL_YEAR",
                                "LINE_ITEM", "POSTING_KEY", "ACCOUNT_TYPE", "SPECIAL_G_L_IND",
                                "TRANSACT_TYPE", "DEBIT_CREDIT", "AMOUNT_IN_LC", "AMOUNT",
                                "TEXT", "VENDOR", "PAYMENT_AMT", "POSTING_DATE"
                            }
                        }
                    }
                };

                return Ok(new
                {
                    Status = "SUCCESS",
                    Message = "Metadata retrieved successfully",
                    Data = metadata
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    Status = "ERROR",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
        }
    }

    public class ZAdvancePaymentRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}