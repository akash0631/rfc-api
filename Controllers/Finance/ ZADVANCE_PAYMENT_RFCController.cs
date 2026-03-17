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
    public class ZadvancePaymentRfcController : BaseController
    {
        [HttpPost]
        public async Task<IHttpActionResult> ExecuteRfc(ZadvancePaymentRfcRequest request)
        {
            try
            {
                // Validate required parameters
                if (string.IsNullOrEmpty(request.I_COMPANY_CODE))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Company Code is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_LOW))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Posting Date Low is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Posting Date High is required",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Validate date format
                DateTime postingDateLow, postingDateHigh;
                if (!DateTime.TryParse(request.I_POSTING_DATE_LOW, out postingDateLow))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Invalid Posting Date Low format",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (!DateTime.TryParse(request.I_POSTING_DATE_HIGH, out postingDateHigh))
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Invalid Posting Date High format",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                if (postingDateLow > postingDateHigh)
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "Posting Date Low cannot be greater than Posting Date High",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Get RFC connection
                RfcDestination rfcDestination = rfcConfigparameters();
                if (rfcDestination == null)
                {
                    return Ok(new
                    {
                        Status = "Error",
                        Message = "SAP RFC connection failed",
                        Data = new { IT_CREDIT = new List<object>() }
                    });
                }

                // Create RFC function
                RfcRepository rfcRepository = rfcDestination.Repository;
                IRfcFunction rfcFunction = rfcRepository.CreateFunction("ZADVANCE_PAYMENT_RFC");

                // Set import parameters
                rfcFunction.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                rfcFunction.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                rfcFunction.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                // Invoke RFC function
                rfcFunction.Invoke(rfcDestination);

                // Get the table result
                IRfcTable itCreditTable = rfcFunction.GetTable("IT_CREDIT");
                List<object> creditData = new List<object>();

                // Process table data with dynamic metadata loop
                if (itCreditTable != null && itCreditTable.Count > 0)
                {
                    RfcTableMetadata tableMetadata = itCreditTable.Metadata;
                    
                    foreach (IRfcStructure row in itCreditTable)
                    {
                        var creditRecord = new Dictionary<string, object>();
                        
                        // Dynamic loop through all fields in the table structure
                        for (int i = 0; i < tableMetadata.FieldCount; i++)
                        {
                            RfcFieldMetadata fieldMetadata = tableMetadata[i];
                            string fieldName = fieldMetadata.Name;
                            
                            // Skip STRUCTURE/TABLE types as specified
                            if (fieldMetadata.DataType == RfcDataType.STRUCTURE || 
                                fieldMetadata.DataType == RfcDataType.TABLE)
                            {
                                continue;
                            }
                            
                            try
                            {
                                object fieldValue = row.GetValue(fieldName);
                                creditRecord[fieldName] = fieldValue ?? string.Empty;
                            }
                            catch (Exception)
                            {
                                creditRecord[fieldName] = string.Empty;
                            }
                        }
                        
                        creditData.Add(creditRecord);
                    }
                }

                return Ok(new
                {
                    Status = "Success",
                    Message = $"RFC executed successfully. Retrieved {creditData.Count} credit records.",
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
                    Message = $"SAP ABAP Runtime Error: {ex.Message}",
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
    }

    public class ZadvancePaymentRfcRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}