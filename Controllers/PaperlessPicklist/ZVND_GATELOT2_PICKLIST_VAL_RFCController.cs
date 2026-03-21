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
    public class ZVND_GATELOT2_PICKLIST_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_GATELOT2_PICKLIST_VAL_RFC")]
        public async Task<IHttpActionResult> ValidatePicklistData([FromBody] ZVND_GATELOT2_PICKLIST_VAL_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = "Request cannot be null",
                        Data = new { ET_VALIDATION_RESULT = new List<object>() }
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_GATELOT2_PICKLIST_VAL_RFC");

                myfun.SetValue("IV_PLANT", request.IV_PLANT);
                myfun.SetValue("IV_PICKLIST_NO", request.IV_PICKLIST_NO);
                myfun.SetValue("IV_LOT_NO", request.IV_LOT_NO);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = new { ET_VALIDATION_RESULT = new List<object>() }
                    });
                }

                IRfcTable validationResultTable = myfun.GetTable("ET_VALIDATION_RESULT");
                
                var validationResults = validationResultTable.AsEnumerable().Select(row =>
                {
                    var result = new Dictionary<string, object>();
                    var metadata = row.GetMetaData();
                    
                    for (int i = 0; i < metadata.FieldCount; i++)
                    {
                        var fieldName = metadata[i].Name;
                        var fieldType = metadata[i].DataType;
                        
                        if (fieldType != RfcDataType.STRUCTURE && fieldType != RfcDataType.TABLE)
                        {
                            result[fieldName] = row.GetString(fieldName);
                        }
                    }
                    
                    return result;
                }).ToList();

                return Ok(new
                {
                    Status = "S",
                    Message = "Picklist validation completed successfully",
                    Data = new { ET_VALIDATION_RESULT = validationResults }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_VALIDATION_RESULT = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_VALIDATION_RESULT = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_VALIDATION_RESULT = new List<object>() }
                });
            }
        }
    }

    public class ZVND_GATELOT2_PICKLIST_VAL_RFCRequest
    {
        public string IV_PLANT { get; set; }
        public string IV_PICKLIST_NO { get; set; }
        public string IV_LOT_NO { get; set; }
    }
}