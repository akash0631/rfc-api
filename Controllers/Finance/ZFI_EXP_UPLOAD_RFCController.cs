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
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_EXP_UPLOAD_RFC")]
        public async Task<HttpResponseMessage> ZFI_EXP_UPLOAD_RFC(ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                // Set input parameters
                if (request.IM_INPUT != null)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = imInputTable.Metadata.LineType.CreateStructure();
                        
                        // Map input structure fields - adjust field names based on actual SAP structure
                        if (!string.IsNullOrEmpty(inputItem.FIELD1))
                            inputRow.SetValue("FIELD1", inputItem.FIELD1);
                        if (!string.IsNullOrEmpty(inputItem.FIELD2))
                            inputRow.SetValue("FIELD2", inputItem.FIELD2);
                        if (!string.IsNullOrEmpty(inputItem.FIELD3))
                            inputRow.SetValue("FIELD3", inputItem.FIELD3);
                        
                        imInputTable.Append(inputRow);
                    }
                }

                if (request.IM_OUTPUT != null)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = imOutputTable.Metadata.LineType.CreateStructure();
                        
                        // Map output structure fields - adjust field names based on actual SAP structure
                        if (!string.IsNullOrEmpty(outputItem.FIELD1))
                            outputRow.SetValue("FIELD1", outputItem.FIELD1);
                        if (!string.IsNullOrEmpty(outputItem.FIELD2))
                            outputRow.SetValue("FIELD2", outputItem.FIELD2);
                        if (!string.IsNullOrEmpty(outputItem.FIELD3))
                            outputRow.SetValue("FIELD3", outputItem.FIELD3);
                        
                        imOutputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    var errorResponse = new
                    {
                        Status = "E",
                        Message = returnMessage
                    };
                    return Request.CreateResponse(HttpStatusCode.BadRequest, errorResponse);
                }

                var successResponse = new
                {
                    Status = returnType,
                    Message = returnMessage
                };

                return Request.CreateResponse(HttpStatusCode.OK, successResponse);
            }
            catch (RfcAbapException ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
            catch (RfcCommunicationException ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
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
        public string FIELD1 { get; set; }
        public string FIELD2 { get; set; }
        public string FIELD3 { get; set; }
    }

    public class ZFI_OUTPUT_STRUC
    {
        public string FIELD1 { get; set; }
        public string FIELD2 { get; set; }
        public string FIELD3 { get; set; }
    }
}