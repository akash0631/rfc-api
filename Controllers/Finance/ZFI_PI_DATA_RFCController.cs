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

                // Set IT_POSTING_LOW table
                IRfcTable itPostingLow = myfun.GetTable("IT_POSTING_LOW");
                if (request.IT_POSTING_LOW != null)
                {
                    foreach (var item in request.IT_POSTING_LOW)
                    {
                        itPostingLow.Append();
                        var fields = typeof(PostingDateRange).GetProperties();
                        foreach (var field in fields)
                        {
                            var value = field.GetValue(item);
                            if (value != null)
                            {
                                itPostingLow.SetValue(field.Name, value.ToString());
                            }
                        }
                    }
                }

                // Set IT_POSTING_HIGH table
                IRfcTable itPostingHigh = myfun.GetTable("IT_POSTING_HIGH");
                if (request.IT_POSTING_HIGH != null)
                {
                    foreach (var item in request.IT_POSTING_HIGH)
                    {
                        itPostingHigh.Append();
                        var fields = typeof(PostingDateRange).GetProperties();
                        foreach (var field in fields)
                        {
                            var value = field.GetValue(item);
                            if (value != null)
                            {
                                itPostingHigh.SetValue(field.Name, value.ToString());
                            }
                        }
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    });
                }

                IRfcTable tbl = myfun.GetTable("IT_FINAL");
                var itFinalData = new List<Dictionary<string, object>>();

                if (tbl != null)
                {
                    foreach (IRfcStructure row in tbl)
                    {
                        var rowData = new Dictionary<string, object>();
                        for (int i = 0; i < row.Metadata.FieldCount; i++)
                        {
                            var field = row.Metadata[i];
                            if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                            {
                                rowData[field.Name] = row.GetString(field.Name);
                            }
                        }
                        itFinalData.Add(rowData);
                    }
                }

                var response = new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        IT_FINAL = itFinalData
                    }
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
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

    public class ZFI_PI_DATA_RFCRequest
    {
        public List<PostingDateRange> IT_POSTING_LOW { get; set; }
        public List<PostingDateRange> IT_POSTING_HIGH { get; set; }
    }

    public class PostingDateRange
    {
        public string SIGN { get; set; }
        public string OPTION { get; set; }
        public string LOW { get; set; }
        public string HIGH { get; set; }
    }
}