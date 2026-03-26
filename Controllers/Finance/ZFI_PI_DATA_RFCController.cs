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
        public async Task<HttpResponseMessage> GetFinancePIData([FromBody] ZFI_PI_DATA_Request request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.IM_DATE))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "IM_DATE parameter is required",
                        Data = new { ET_PI_DATA = new List<object>() }
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");

                myfun.SetValue("IM_DATE", request.IM_DATE);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN["TYPE"].ToString() == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN["MESSAGE"].ToString(),
                        Data = new { ET_PI_DATA = new List<object>() }
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_PI_DATA");
                var piData = new List<Dictionary<string, object>>();

                foreach (IRfcStructure row in tbl.AsEnumerable())
                {
                    var rowDict = new Dictionary<string, object>();
                    for (int i = 0; i < row.Count; i++)
                    {
                        var fieldName = row.GetMetadata().FieldMetadata[i].Name;
                        var fieldMetadata = row.GetMetadata().FieldMetadata[i];
                        
                        if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                        {
                            rowDict[fieldName] = row[fieldName]?.ToString() ?? "";
                        }
                    }
                    piData.Add(rowDict);
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Finance PI data retrieved successfully",
                    Data = new { ET_PI_DATA = piData }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_PI_DATA = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_PI_DATA = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_PI_DATA = new List<object>() }
                });
            }
        }
    }

    public class ZFI_PI_DATA_Request
    {
        public string IM_DATE { get; set; }
    }
}