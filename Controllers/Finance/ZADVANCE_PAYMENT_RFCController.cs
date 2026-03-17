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
        public async Task<IHttpActionResult> GetAdvancePayment([FromBody] ZAdvancePaymentRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new
                    {
                        Status = "Error",
                        Message = "Request body cannot be null",
                        Data = (object)null
                    });
                }

                if (string.IsNullOrWhiteSpace(request.I_COMPANY_CODE))
                {
                    return Json(new
                    {
                        Status = "Error",
                        Message = "Company Code (I_COMPANY_CODE) is required",
                        Data = (object)null
                    });
                }

                if (string.IsNullOrWhiteSpace(request.I_POSTING_DATE_LOW))
                {
                    return Json(new
                    {
                        Status = "Error",
                        Message = "Posting Date Low (I_POSTING_DATE_LOW) is required",
                        Data = (object)null
                    });
                }

                if (string.IsNullOrWhiteSpace(request.I_POSTING_DATE_HIGH))
                {
                    return Json(new
                    {
                        Status = "Error",
                        Message = "Posting Date High (I_POSTING_DATE_HIGH) is required",
                        Data = (object)null
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction rfcFunction = rfcrep.CreateFunction("ZADVANCE_PAYMENT_RFC");

                rfcFunction.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                rfcFunction.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                rfcFunction.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                rfcFunction.Invoke(dest);

                var itCreditTable = rfcFunction.GetTable("IT_CREDIT");
                var creditData = new List<dynamic>();

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

                return Json(new
                {
                    Status = "Success",
                    Message = "Advance payment data retrieved successfully",
                    Data = new
                    {
                        IT_CREDIT = creditData
                    }
                });
            }
            catch (RfcCommunicationException rfcCommEx)
            {
                return Json(new
                {
                    Status = "Error",
                    Message = $"RFC Communication Error: {rfcCommEx.Message}",
                    Data = (object)null
                });
            }
            catch (RfcLogonException rfcLogonEx)
            {
                return Json(new
                {
                    Status = "Error",
                    Message = $"RFC Logon Error: {rfcLogonEx.Message}",
                    Data = (object)null
                });
            }
            catch (RfcAbapRuntimeException rfcRuntimeEx)
            {
                return Json(new
                {
                    Status = "Error",
                    Message = $"RFC Runtime Error: {rfcRuntimeEx.Message}",
                    Data = (object)null
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = "Error",
                    Message = $"Unexpected error occurred: {ex.Message}",
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