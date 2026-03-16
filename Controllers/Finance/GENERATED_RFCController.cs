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
        public async Task<HttpResponseMessage> ProcessGENERATED_RFC([FromBody] GENERATED_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "Error",
                        Message = "Request body cannot be null",
                        Data = (object)null
                    });
                }

                if (string.IsNullOrWhiteSpace(request.I_COMPANY_CODE))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "Error",
                        Message = "I_COMPANY_CODE is required",
                        Data = (object)null
                    });
                }

                if (string.IsNullOrWhiteSpace(request.I_POSTING_DATE_LOW))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "Error",
                        Message = "I_POSTING_DATE_LOW is required",
                        Data = (object)null
                    });
                }

                var rfcParameters = await rfcConfigparameters();
                if (rfcParameters == null)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status = "Error",
                        Message = "RFC configuration parameters not available",
                        Data = (object)null
                    });
                }

                IRfcFunction rfcFunction = rfcParameters.Repository.CreateFunction("GENERATED_RFC");

                rfcFunction.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                rfcFunction.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                
                if (!string.IsNullOrWhiteSpace(request.I_POSTING_DATE_HIGH))
                {
                    rfcFunction.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);
                }

                rfcFunction.Invoke(rfcParameters.Destination);

                IRfcTable itCreditTable = rfcFunction.GetTable("IT_CREDIT");
                List<dynamic> creditList = new List<dynamic>();

                if (itCreditTable != null)
                {
                    foreach (IRfcStructure row in itCreditTable)
                    {
                        var creditItem = new
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

                var response = new
                {
                    Status = "Success",
                    Message = $"GENERATED_RFC executed successfully. Retrieved {creditList.Count} credit records.",
                    Data = new
                    {
                        IT_CREDIT = creditList
                    }
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (RfcCommunicationException rfcCommEx)
            {
                return Request.CreateResponse(HttpStatusCode.ServiceUnavailable, new
                {
                    Status = "Error",
                    Message = $"SAP RFC Communication Error: {rfcCommEx.Message}",
                    Data = (object)null
                });
            }
            catch (RfcAbapException rfcAbapEx)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    Status = "Error",
                    Message = $"SAP ABAP Error: {rfcAbapEx.Key} - {rfcAbapEx.Message}",
                    Data = (object)null
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "Error",
                    Message = $"Unexpected error: {ex.Message}",
                    Data = (object)null
                });
            }
        }
    }

    public class GENERATED_RFCRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}