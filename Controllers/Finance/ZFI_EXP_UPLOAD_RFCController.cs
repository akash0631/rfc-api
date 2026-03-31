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
        public async Task<IHttpActionResult> ZFI_EXP_UPLOAD_RFC([FromBody] ZFI_EXP_UPLOAD_RFC_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Content(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request cannot be null" });
                }

                // SAP Connector Pattern - mandatory exact pattern
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                // Set input parameters
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    foreach (var item in request.IM_INPUT)
                    {
                        var row = inputTable.Metadata.LineType.CreateStructure();
                        // Map properties from item to row based on ZFI_INPUT_STRUC_TT structure
                        // Add specific field mappings here based on actual structure
                        inputTable.Append(row);
                    }
                }

                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        var row = outputTable.Metadata.LineType.CreateStructure();
                        // Map properties from item to row based on ZFI_OUTPUT_STRUC_TT structure
                        // Add specific field mappings here based on actual structure
                        outputTable.Append(row);
                    }
                }

                // Invoke the RFC
                myfun.Invoke(dest);

                // Get the return structure
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                // Check for errors in EX_RETURN
                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Content(HttpStatusCode.BadRequest, new { Status = "E", Message = returnMessage });
                }

                return Ok(new { Status = returnType, Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZFI_EXP_UPLOAD_RFC_Request
    {
        public List<ZFI_INPUT_STRUC_TT> IM_INPUT { get; set; }
        public List<ZFI_OUTPUT_STRUC_TT> IM_OUTPUT { get; set; }
    }

    public class ZFI_INPUT_STRUC_TT
    {
        // Add properties based on actual SAP structure fields
        // Example properties - replace with actual structure fields:
        public string VENDOR_ID { get; set; }
        public string INVOICE_NUMBER { get; set; }
        public decimal AMOUNT { get; set; }
        public string CURRENCY { get; set; }
        public DateTime INVOICE_DATE { get; set; }
        public string DOCUMENT_TYPE { get; set; }
        public string COMPANY_CODE { get; set; }
    }

    public class ZFI_OUTPUT_STRUC_TT
    {
        // Add properties based on actual SAP structure fields
        // Example properties - replace with actual structure fields:
        public string DOCUMENT_NUMBER { get; set; }
        public string FISCAL_YEAR { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
        public string POSTING_DATE { get; set; }
    }
}