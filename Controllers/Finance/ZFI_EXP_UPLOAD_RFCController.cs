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
    [RoutePrefix("api")]
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("ZFI_EXP_UPLOAD_RFC")]
        public async Task<HttpResponseMessage> ZFI_EXP_UPLOAD_RFC([FromBody] ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request body cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                // Set IM_INPUT table
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    foreach (var item in request.IM_INPUT)
                    {
                        inputTable.Append();
                        inputTable.CurrentRow.SetValue("BUKRS", item.BUKRS ?? "");
                        inputTable.CurrentRow.SetValue("LIFNR", item.LIFNR ?? "");
                        inputTable.CurrentRow.SetValue("XBLNR", item.XBLNR ?? "");
                        inputTable.CurrentRow.SetValue("BLDAT", item.BLDAT ?? "");
                        inputTable.CurrentRow.SetValue("BUDAT", item.BUDAT ?? "");
                        inputTable.CurrentRow.SetValue("WAERS", item.WAERS ?? "");
                        inputTable.CurrentRow.SetValue("WRBTR", item.WRBTR ?? "");
                        inputTable.CurrentRow.SetValue("BKTXT", item.BKTXT ?? "");
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        outputTable.Append();
                        outputTable.CurrentRow.SetValue("FIELD1", item.FIELD1 ?? "");
                        outputTable.CurrentRow.SetValue("FIELD2", item.FIELD2 ?? "");
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE")?.ToString() ?? "";
                string returnMessage = EX_RETURN.GetValue("MESSAGE")?.ToString() ?? "";

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = returnMessage });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "S", Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZFI_EXP_UPLOAD_RFCRequest
    {
        public List<IM_INPUT_Item> IM_INPUT { get; set; }
        public List<IM_OUTPUT_Item> IM_OUTPUT { get; set; }
    }

    public class IM_INPUT_Item
    {
        public string BUKRS { get; set; }
        public string LIFNR { get; set; }
        public string XBLNR { get; set; }
        public string BLDAT { get; set; }
        public string BUDAT { get; set; }
        public string WAERS { get; set; }
        public string WRBTR { get; set; }
        public string BKTXT { get; set; }
    }

    public class IM_OUTPUT_Item
    {
        public string FIELD1 { get; set; }
        public string FIELD2 { get; set; }
    }
}