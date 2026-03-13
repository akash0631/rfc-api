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
    public class ZTEST_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZTEST_RFC")]
        public IHttpActionResult ZTEST_RFC([FromBody] ZTEST_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZTEST_RFC");

                myfun.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                myfun.SetValue("I_DATE", request.I_DATE);

                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = new { ET_RESULT = new List<object>() }
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_RESULT");
                var resultData = new List<Dictionary<string, object>>();

                if (tbl != null)
                {
                    resultData = tbl.AsEnumerable().Select(row =>
                    {
                        var rowDict = new Dictionary<string, object>();
                        var metadata = row.GetMetadata();
                        
                        for (int i = 0; i < metadata.FieldCount; i++)
                        {
                            var field = metadata[i];
                            if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                            {
                                rowDict[field.Name] = row.GetValue(field.Name);
                            }
                        }
                        
                        return rowDict;
                    }).ToList();
                }

                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new { ET_RESULT = resultData }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_RESULT = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_RESULT = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_RESULT = new List<object>() }
                });
            }
        }
    }

    public class ZTEST_RFCRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_DATE { get; set; }
    }
}