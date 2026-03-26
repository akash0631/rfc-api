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
    public class ZFI_PI_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_PI_DATA_RFC")]
        public IHttpActionResult GetFinancePIData(ZFI_PI_DATA_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");
                
                myfun.SetValue("IT_POSTING_LOW", request.IT_POSTING_LOW);
                myfun.SetValue("IT_POSTING_HIGH", request.IT_POSTING_HIGH);
                
                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = new { IT_FINAL = new object[0] }
                    });
                }
                
                IRfcTable tbl = myfun.GetTable("IT_FINAL");
                
                var result = tbl.AsEnumerable().Select(row =>
                {
                    var dynamicRow = new Dictionary<string, object>();
                    
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            dynamicRow[field.Name] = row.GetValue(field.Name);
                        }
                    }
                    
                    return dynamicRow;
                }).ToList();
                
                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new { IT_FINAL = result }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new object[0] }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new object[0] }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new object[0] }
                });
            }
        }
    }
    
    public class ZFI_PI_DATA_Request
    {
        public string IT_POSTING_LOW { get; set; }
        public string IT_POSTING_HIGH { get; set; }
    }
}