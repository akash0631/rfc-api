using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZFI_PI_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_PI_DATA_RFC")]
        public IHttpActionResult GetFinancialPostingData([FromBody] ZFI_PI_DATA_RFC_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");

                if (request.IT_POSTING_LOW != null)
                {
                    IRfcTable postingLowTable = myfun.GetTable("IT_POSTING_LOW");
                    foreach (var item in request.IT_POSTING_LOW)
                    {
                        postingLowTable.Append();
                        var itemType = item.GetType();
                        var properties = itemType.GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(item);
                            if (value != null)
                            {
                                postingLowTable.SetValue(prop.Name.ToUpper(), value.ToString());
                            }
                        }
                    }
                }

                if (request.IT_POSTING_HIGH != null)
                {
                    IRfcTable postingHighTable = myfun.GetTable("IT_POSTING_HIGH");
                    foreach (var item in request.IT_POSTING_HIGH)
                    {
                        postingHighTable.Append();
                        var itemType = item.GetType();
                        var properties = itemType.GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(item);
                            if (value != null)
                            {
                                postingHighTable.SetValue(prop.Name.ToUpper(), value.ToString());
                            }
                        }
                    }
                }

                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = new { IT_FINAL = new List<object>() }
                    });
                }

                IRfcTable finalTable = myfun.GetTable("IT_FINAL");
                var finalData = new List<Dictionary<string, object>>();

                if (finalTable.Count > 0)
                {
                    var metadata = finalTable.GetElementMetadata();
                    
                    foreach (IRfcStructure row in finalTable)
                    {
                        var rowData = new Dictionary<string, object>();
                        
                        for (int i = 0; i < metadata.FieldCount; i++)
                        {
                            var fieldMetadata = metadata[i];
                            var fieldName = fieldMetadata.Name;
                            var fieldType = fieldMetadata.DataType;
                            
                            if (fieldType != RfcDataType.STRUCTURE && fieldType != RfcDataType.TABLE)
                            {
                                var fieldValue = row.GetString(fieldName);
                                rowData[fieldName] = fieldValue;
                            }
                        }
                        
                        finalData.Add(rowData);
                    }
                }

                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new { IT_FINAL = finalData }
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

    public class ZFI_PI_DATA_RFC_Request
    {
        public List<dynamic> IT_POSTING_LOW { get; set; }
        public List<dynamic> IT_POSTING_HIGH { get; set; }
    }
}