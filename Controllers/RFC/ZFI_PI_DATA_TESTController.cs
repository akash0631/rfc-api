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
    public class ZFI_PI_DATA_TESTController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_PI_DATA_TEST")]
        public async Task<HttpResponseMessage> ZFI_PI_DATA_TEST(ZFI_PI_DATA_TESTRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_TEST");

                myfun.SetValue("IV_BUKRS", request.IV_BUKRS);
                myfun.SetValue("IV_GJAHR", request.IV_GJAHR);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    var errorResponse = new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    };
                    return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
                }

                IRfcTable tbl = myfun.GetTable("GT_DATA");
                var gtData = tbl.AsEnumerable().Select(row =>
                {
                    var rowData = new Dictionary<string, object>();
                    var metadata = row.Metadata;
                    
                    for (int i = 0; i < metadata.FieldCount; i++)
                    {
                        var field = metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            rowData[field.Name] = row.GetValue(field.Name);
                        }
                    }
                    return rowData;
                }).ToList();

                var response = new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        GT_DATA = gtData
                    }
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (RfcAbapException ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
            }
            catch (RfcCommunicationException ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.OK, errorResponse);
            }
        }
    }

    public class ZFI_PI_DATA_TESTRequest
    {
        public string IV_BUKRS { get; set; }
        public string IV_GJAHR { get; set; }
    }
}