using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public async Task<HttpResponseMessage> ZFI_PI_DATA_RFC(ZFI_PI_DATA_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");

                // Set IT_POSTING_LOW table parameter
                IRfcTable postingLowTable = myfun.GetTable("IT_POSTING_LOW");
                if (request.IT_POSTING_LOW != null)
                {
                    foreach (var item in request.IT_POSTING_LOW)
                    {
                        IRfcStructure row = postingLowTable.Metadata.LineType.CreateStructure();
                        foreach (var prop in item.GetType().GetProperties())
                        {
                            var value = prop.GetValue(item);
                            if (value != null)
                            {
                                row.SetValue(prop.Name, value.ToString());
                            }
                        }
                        postingLowTable.Append(row);
                    }
                }

                // Set IT_POSTING_HIGH table parameter
                IRfcTable postingHighTable = myfun.GetTable("IT_POSTING_HIGH");
                if (request.IT_POSTING_HIGH != null)
                {
                    foreach (var item in request.IT_POSTING_HIGH)
                    {
                        IRfcStructure row = postingHighTable.Metadata.LineType.CreateStructure();
                        foreach (var prop in item.GetType().GetProperties())
                        {
                            var value = prop.GetValue(item);
                            if (value != null)
                            {
                                row.SetValue(prop.Name, value.ToString());
                            }
                        }
                        postingHighTable.Append(row);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                if (EX_RETURN.GetValue("TYPE").ToString() == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetValue("MESSAGE").ToString()
                    });
                }

                IRfcTable finalTable = myfun.GetTable("IT_FINAL");
                var finalData = finalTable.AsEnumerable().Select(row => 
                {
                    var result = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            result[field.Name] = row.GetValue(i);
                        }
                    }
                    return result;
                }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        IT_FINAL = finalData
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
            catch (CommunicationException ex)
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

    public class ZFI_PI_DATA_RFCRequest
    {
        public List<object> IT_POSTING_LOW { get; set; }
        public List<object> IT_POSTING_HIGH { get; set; }
    }
}