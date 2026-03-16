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
        public async Task<IHttpActionResult> GetGeneratedRfc([FromBody] GeneratedRfcRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request cannot be null");
                }

                if (string.IsNullOrEmpty(request.I_COMPANY_CODE))
                {
                    return BadRequest("Company Code is required");
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_LOW))
                {
                    return BadRequest("Posting Date Low is required");
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
                {
                    return BadRequest("Posting Date High is required");
                }

                var rfcConfigParams = rfcConfigparameters();
                if (rfcConfigParams == null)
                {
                    return InternalServerError(new Exception("RFC configuration parameters not available"));
                }

                IRfcFunction function = rfcConfigParams.Repository.CreateFunction("GENERATED_RFC");
                if (function == null)
                {
                    return InternalServerError(new Exception("RFC function GENERATED_RFC not found"));
                }

                function.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                function.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                function.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                function.Invoke(rfcConfigParams);

                IRfcTable itCreditTable = function.GetTable("IT_CREDIT");
                List<ITCreditItem> itCreditList = new List<ITCreditItem>();

                if (itCreditTable != null)
                {
                    for (int i = 0; i < itCreditTable.RowCount; i++)
                    {
                        itCreditTable.CurrentIndex = i;
                        
                        ITCreditItem item = new ITCreditItem
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

                        itCreditList.Add(item);
                    }
                }

                var response = new
                {
                    Status = "Success",
                    Message = "Data retrieved successfully",
                    Data = new
                    {
                        IT_CREDIT = itCreditList
                    }
                };

                return Ok(response);
            }
            catch (RfcCommunicationException ex)
            {
                return InternalServerError(new Exception($"RFC Communication Error: {ex.Message}"));
            }
            catch (RfcLogonException ex)
            {
                return InternalServerError(new Exception($"RFC Logon Error: {ex.Message}"));
            }
            catch (RfcAbapRuntimeException ex)
            {
                return InternalServerError(new Exception($"SAP Runtime Error: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"General Error: {ex.Message}"));
            }
        }
    }

    public class GeneratedRfcRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
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