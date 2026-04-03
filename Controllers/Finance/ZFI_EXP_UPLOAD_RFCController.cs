using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using System.Linq;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_EXP_UPLOAD_RFC")]
        public async Task<IHttpActionResult> ZFI_EXP_UPLOAD_RFC([FromBody] ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Request body is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                // Set IM_INPUT parameter
                if (request.IM_INPUT != null)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = inputTable.Metadata.LineType.CreateStructure();
                        
                        foreach (var prop in inputItem.GetType().GetProperties())
                        {
                            var value = prop.GetValue(inputItem);
                            if (value != null && inputRow.Metadata.ContainsField(prop.Name))
                            {
                                inputRow.SetValue(prop.Name, value);
                            }
                        }
                        inputTable.Append(inputRow);
                    }
                }

                // Set IM_OUTPUT parameter
                if (request.IM_OUTPUT != null)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = outputTable.Metadata.LineType.CreateStructure();
                        
                        foreach (var prop in outputItem.GetType().GetProperties())
                        {
                            var value = prop.GetValue(outputItem);
                            if (value != null && outputRow.Metadata.ContainsField(prop.Name))
                            {
                                outputRow.SetValue(prop.Name, value);
                            }
                        }
                        outputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);

                // Get EX_RETURN table
                IRfcTable exReturnTable = myfun.GetTable("EX_RETURN");
                
                var returnData = exReturnTable.AsEnumerable().Select(row =>
                {
                    var result = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            result[field.Name] = row[i].GetValue();
                        }
                    }
                    return result;
                }).ToList();

                // Check for errors in EX_RETURN
                var errorRows = returnData.Where(r => r.ContainsKey("TYPE") && r["TYPE"]?.ToString() == "E").ToList();
                if (errorRows.Any())
                {
                    var errorMessage = errorRows.FirstOrDefault()?.ContainsKey("MESSAGE") == true 
                        ? errorRows.First()["MESSAGE"]?.ToString() 
                        : "Error occurred during processing";
                    
                    return Json(new { Status = "E", Message = errorMessage, Data = new { EX_RETURN = returnData } });
                }

                return Json(new { Status = "S", Message = "Success", Data = new { EX_RETURN = returnData } });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZFI_EXP_UPLOAD_RFCRequest
    {
        public List<ZFI_INPUT_STRUC> IM_INPUT { get; set; }
        public List<ZFI_OUTPUT_STRUC> IM_OUTPUT { get; set; }
    }

    public class ZFI_INPUT_STRUC
    {
        // Add properties based on SAP structure ZFI_INPUT_STRUC_TT
    }

    public class ZFI_OUTPUT_STRUC
    {
        // Add properties based on SAP structure ZFI_OUTPUT_STRUC_TT
    }
}