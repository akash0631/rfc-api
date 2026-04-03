using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using System.Linq;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_EXP_UPLOAD_RFC")]
        public async Task<HttpResponseMessage> ZFI_EXP_UPLOAD_RFC([FromBody] ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request body cannot be null",
                        Data = new { EX_RETURN = new List<object>() }
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                if (request.IM_INPUT != null)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = imInputTable.Metadata.LineType.CreateStructure();
                        var properties = inputItem.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(inputItem);
                            if (value != null)
                            {
                                inputRow.SetValue(prop.Name, value);
                            }
                        }
                        imInputTable.Append(inputRow);
                    }
                }

                if (request.IM_OUTPUT != null)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = imOutputTable.Metadata.LineType.CreateStructure();
                        var properties = outputItem.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(outputItem);
                            if (value != null)
                            {
                                outputRow.SetValue(prop.Name, value);
                            }
                        }
                        imOutputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);

                IRfcTable exReturnTable = myfun.GetTable("EX_RETURN");
                var returnData = exReturnTable.AsEnumerable().Select(row =>
                {
                    var result = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            result[field.Name] = row.GetValue(field.Name);
                        }
                    }
                    return result;
                }).ToList();

                var hasError = returnData.Any(item => item.ContainsKey("TYPE") && item["TYPE"]?.ToString() == "E");
                if (hasError)
                {
                    var errorMessage = returnData.Where(item => item.ContainsKey("TYPE") && item["TYPE"]?.ToString() == "E")
                                                .Select(item => item.ContainsKey("MESSAGE") ? item["MESSAGE"]?.ToString() : "Unknown error")
                                                .FirstOrDefault() ?? "Error occurred during RFC execution";

                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = errorMessage,
                        Data = new { EX_RETURN = returnData }
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Finance expense upload processed successfully",
                    Data = new { EX_RETURN = returnData }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { EX_RETURN = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { EX_RETURN = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { EX_RETURN = new List<object>() }
                });
            }
        }
    }

    public class ZFI_EXP_UPLOAD_RFCRequest
    {
        public List<ZFI_INPUT_STRUC> IM_INPUT { get; set; }
        public List<ZFI_OUTPUT_STRUC> IM_OUTPUT { get; set; }
    }

    public class ZFI_INPUT_STRUC
    {
        public string BUKRS { get; set; }
        public string LIFNR { get; set; }
        public string BLDAT { get; set; }
        public string BUDAT { get; set; }
        public string XBLNR { get; set; }
        public string WRBTR { get; set; }
        public string WAERS { get; set; }
        public string BKTXT { get; set; }
        public string KOSTL { get; set; }
        public string SAKNR { get; set; }
        public string SGTXT { get; set; }
    }

    public class ZFI_OUTPUT_STRUC
    {
        public string BELNR { get; set; }
        public string GJAHR { get; set; }
        public string BUKRS { get; set; }
        public string XBLNR { get; set; }
        public string MESSAGE { get; set; }
        public string STATUS { get; set; }
    }
}