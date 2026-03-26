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
        public IHttpActionResult GetFinancePIData(ZFI_PI_DATA_RFCRequest request)
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
                        Data = new { IT_FINAL = new List<dynamic>() }
                    });
                }

                IRfcTable itFinalTable = myfun.GetTable("IT_FINAL");
                
                var itFinalData = itFinalTable.AsEnumerable().Select(row =>
                {
                    var rowData = new Dictionary<string, object>();
                    var metadata = row.GetMetadata();
                    
                    for (int i = 0; i < metadata.FieldCount; i++)
                    {
                        var field = metadata.GetField(i);
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            rowData[field.Name] = row.GetValue(field.Name);
                        }
                    }
                    return rowData;
                }).ToList();

                return Ok(new
                {
                    Status = "S",
                    Message = "Data retrieved successfully",
                    Data = new { IT_FINAL = itFinalData }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new List<dynamic>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new List<dynamic>() }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { IT_FINAL = new List<dynamic>() }
                });
            }
        }
    }

    public class ZFI_PI_DATA_RFCRequest
    {
        public string IT_POSTING_LOW { get; set; }
        public string IT_POSTING_HIGH { get; set; }
    }
}