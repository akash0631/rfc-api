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
    public class ZTEST_FINAL_FIX_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZTEST_FINAL_FIX_RFC")]
        public HttpResponseMessage ZTEST_FINAL_FIX_RFC(ZTEST_FINAL_FIX_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZTEST_FINAL_FIX_RFC");
                
                myfun.SetValue("IV_EBELN", request.IV_EBELN);
                myfun.SetValue("IV_EBELP", request.IV_EBELP);
                
                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    });
                }
                
                IRfcTable tbl = myfun.GetTable("ET_ITEMS");
                
                var etItems = tbl.AsEnumerable().Select(row =>
                {
                    var item = new Dictionary<string, object>();
                    var metadata = row.GetMetadata();
                    
                    for (int i = 0; i < metadata.FieldCount; i++)
                    {
                        var field = metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            item[field.Name] = row.GetValue(field.Name);
                        }
                    }
                    
                    return item;
                }).ToList();
                
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_ITEMS = etItems
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }
    
    public class ZTEST_FINAL_FIX_RFCRequest
    {
        public string IV_EBELN { get; set; }
        public string IV_EBELP { get; set; }
    }
}