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
    public class ZADVANCE_PAYMENT_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZADVANCE_PAYMENT_RFC")]
        public async Task<IHttpActionResult> GetAdvancePaymentInformation(ZAdvancePaymentRequest request)
        {
            try
            {
                var rfcFunction = rfcConfigparameters.GetFunction("ZADVANCE_PAYMENT_RFC");
                
                rfcFunction.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE ?? "");
                rfcFunction.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW ?? "");
                rfcFunction.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH ?? "");
                
                rfcFunction.Invoke(rfcConfigparameters);
                
                var exReturn = rfcFunction.GetStructure("EX_RETURN");
                if (exReturn != null && exReturn.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = exReturn.GetString("MESSAGE"),
                        Data = new { ET_ADVANCE_PAYMENT = new object[0] }
                    });
                }
                
                var etAdvancePaymentTable = rfcFunction.GetTable("ET_ADVANCE_PAYMENT");
                var advancePaymentList = new List<Dictionary<string, object>>();
                
                if (etAdvancePaymentTable != null)
                {
                    foreach (IRfcStructure row in etAdvancePaymentTable)
                    {
                        var rowData = new Dictionary<string, object>();
                        for (int i = 0; i < row.Metadata.FieldCount; i++)
                        {
                            var fieldMetadata = row.Metadata[i];
                            if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                            {
                                rowData[fieldMetadata.Name] = row.GetValue(fieldMetadata.Name);
                            }
                        }
                        advancePaymentList.Add(rowData);
                    }
                }
                
                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_ADVANCE_PAYMENT = advancePaymentList
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
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