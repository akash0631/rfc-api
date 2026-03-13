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
        public async Task<IHttpActionResult> GetAdvancePaymentDocuments(ZADVANCE_PAYMENT_RFCRequest request)
        {
            try
            {
                var destination = RfcDestinationManager.GetDestination(rfcConfigparameters("192.168.144.174", "210"));
                var function = destination.Repository.CreateFunction("ZADVANCE_PAYMENT_RFC");

                // Set import parameters
                function.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                function.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                function.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                // Invoke RFC
                function.Invoke(destination);

                // Check return parameter
                var exReturn = function.GetStructure("EX_RETURN");
                if (exReturn != null && exReturn.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = exReturn.GetString("MESSAGE"),
                        Data = new { IT_FINAL = new List<object>() }
                    });
                }

                // Get IT_FINAL table
                var itFinalTable = function.GetTable("IT_FINAL");
                var resultList = new List<Dictionary<string, object>>();

                if (itFinalTable != null && itFinalTable.Count > 0)
                {
                    var metadata = itFinalTable.Metadata;
                    var fieldNames = new List<string>();

                    // Get field names, skip STRUCTURE/TABLE types
                    for (int i = 0; i < metadata.FieldCount; i++)
                    {
                        var field = metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            fieldNames.Add(field.Name);
                        }
                    }

                    // Process each row
                    foreach (IRfcStructure row in itFinalTable)
                    {
                        var rowData = new Dictionary<string, object>();
                        foreach (var fieldName in fieldNames)
                        {
                            rowData[fieldName] = row.GetValue(fieldName)?.ToString() ?? "";
                        }
                        resultList.Add(rowData);
                    }
                }

                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new { IT_FINAL = resultList }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new List<object>() }
                });
            }
        }
    }

    public class ZADVANCE_PAYMENT_RFCRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}