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
        public async Task<IHttpActionResult> ExecuteAdvancePayment([FromBody] AdvancePaymentRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        Status = "Error",
                        Message = "Request cannot be null",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Validate required parameters
                if (string.IsNullOrEmpty(request.I_COMPANY_CODE))
                {
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        Status = "Error",
                        Message = "Company Code is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_LOW))
                {
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        Status = "Error",
                        Message = "Posting Date Low is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Validate date format
                DateTime postingDateLow;
                if (!DateTime.TryParseExact(request.I_POSTING_DATE_LOW, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out postingDateLow))
                {
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        Status = "Error",
                        Message = "Invalid Posting Date Low format. Expected: YYYYMMDD",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                DateTime postingDateHigh = DateTime.Now;
                if (!string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
                {
                    if (!DateTime.TryParseExact(request.I_POSTING_DATE_HIGH, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out postingDateHigh))
                    {
                        return Content(HttpStatusCode.BadRequest, new
                        {
                            Status = "Error",
                            Message = "Invalid Posting Date High format. Expected: YYYYMMDD",
                            Data = new { IT_CREDIT = new List<object>() }
                        });
                    }

                    // Validate date range
                    if (postingDateHigh < postingDateLow)
                    {
                        return Content(HttpStatusCode.BadRequest, new
                        {
                            Status = "Error",
                            Message = "Posting Date High cannot be earlier than Posting Date Low",
                            Data = new { IT_CREDIT = new List<object>() }
                        });
                    }
                }

                // Execute RFC call
                var rfcResult = await ExecuteRfcCall(request);

                if (rfcResult.Status == "Error")
                {
                    return Content(HttpStatusCode.InternalServerError, rfcResult);
                }

                return Ok(rfcResult);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    Status = "Error",
                    Message = $"Internal server error: {ex.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                });
            }
        }

        private async Task<dynamic> ExecuteRfcCall(AdvancePaymentRequest request)
        {
            try
            {
                var rfcConfigParameters = BaseController.rfcConfigparameters();
                
                if (rfcConfigParameters == null)
                {
                    return new
                    {
                        Status = "Error",
                        Message = "RFC configuration parameters not available",
                        Data = new { IT_CREDIT = new List<object>() }
                    };
                }

                RfcDestination destination = RfcDestinationManager.GetDestination(rfcConfigParameters);
                IRfcFunction function = destination.Repository.CreateFunction("ZADVANCE_PAYMENT_RFC");

                if (function == null)
                {
                    return new
                    {
                        Status = "Error",
                        Message = "RFC function ZADVANCE_PAYMENT_RFC not found",
                        Data = new { IT_CREDIT = new List<object>() }
                    };
                }

                // Set import parameters
                function.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                function.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                
                if (!string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
                {
                    function.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);
                }

                // Execute RFC function
                function.Invoke(destination);

                // Get table data
                IRfcTable creditTable = function.GetTable("IT_CREDIT");
                List<object> creditList = new List<object>();

                if (creditTable != null && creditTable.RowCount > 0)
                {
                    foreach (IRfcStructure row in creditTable)
                    {
                        var creditItem = new Dictionary<string, object>();
                        
                        // Dynamic metadata loop - extract all available fields
                        for (int i = 0; i < row.Metadata.FieldCount; i++)
                        {
                            var fieldMeta = row.Metadata[i];
                            string fieldName = fieldMeta.Name;
                            
                            // Skip STRUCTURE/TABLE types as specified
                            if (fieldMeta.DataType != RfcDataType.STRUCTURE && 
                                fieldMeta.DataType != RfcDataType.TABLE)
                            {
                                try
                                {
                                    object fieldValue = row.GetValue(fieldName);
                                    creditItem[fieldName] = fieldValue?.ToString() ?? string.Empty;
                                }
                                catch
                                {
                                    creditItem[fieldName] = string.Empty;
                                }
                            }
                        }
                        
                        creditList.Add(creditItem);
                    }
                }

                return new
                {
                    Status = "Success",
                    Message = $"Successfully retrieved {creditList.Count} advance payment records",
                    Data = new { IT_CREDIT = creditList }
                };
            }
            catch (RfcCommunicationException rfcEx)
            {
                return new
                {
                    Status = "Error",
                    Message = $"RFC Communication Error: {rfcEx.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                };
            }
            catch (RfcLogonException rfcEx)
            {
                return new
                {
                    Status = "Error",
                    Message = $"RFC Logon Error: {rfcEx.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                };
            }
            catch (RfcAbapRuntimeException rfcEx)
            {
                return new
                {
                    Status = "Error",
                    Message = $"RFC ABAP Runtime Error: {rfcEx.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Status = "Error",
                    Message = $"Unexpected error during RFC execution: {ex.Message}",
                    Data = new { IT_CREDIT = new List<object>() }
                };
            }
        }
    }

    public class AdvancePaymentRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}