using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor
{
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public async Task<HttpResponseMessage> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Invalid request data" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT table parameter
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = imInputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(inputItem.EBELN))
                            inputRow.SetValue("EBELN", inputItem.EBELN);
                        if (!string.IsNullOrEmpty(inputItem.EBELP))
                            inputRow.SetValue("EBELP", inputItem.EBELP);
                        if (!string.IsNullOrEmpty(inputItem.MATNR))
                            inputRow.SetValue("MATNR", inputItem.MATNR);
                        if (!string.IsNullOrEmpty(inputItem.COLOR))
                            inputRow.SetValue("COLOR", inputItem.COLOR);
                        if (!string.IsNullOrEmpty(inputItem.MENGE))
                            inputRow.SetValue("MENGE", inputItem.MENGE);
                        if (!string.IsNullOrEmpty(inputItem.MEINS))
                            inputRow.SetValue("MEINS", inputItem.MEINS);
                        if (!string.IsNullOrEmpty(inputItem.NETPR))
                            inputRow.SetValue("NETPR", inputItem.NETPR);
                        if (!string.IsNullOrEmpty(inputItem.EINDT))
                            inputRow.SetValue("EINDT", inputItem.EINDT);
                        
                        imInputTable.Append(inputRow);
                    }
                }

                // Set IM_OUTPUT table parameter
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = imOutputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(outputItem.EBELN))
                            outputRow.SetValue("EBELN", outputItem.EBELN);
                        if (!string.IsNullOrEmpty(outputItem.EBELP))
                            outputRow.SetValue("EBELP", outputItem.EBELP);
                        if (!string.IsNullOrEmpty(outputItem.MATNR))
                            outputRow.SetValue("MATNR", outputItem.MATNR);
                        if (!string.IsNullOrEmpty(outputItem.COLOR))
                            outputRow.SetValue("COLOR", outputItem.COLOR);
                        if (!string.IsNullOrEmpty(outputItem.MENGE))
                            outputRow.SetValue("MENGE", outputItem.MENGE);
                        if (!string.IsNullOrEmpty(outputItem.MEINS))
                            outputRow.SetValue("MEINS", outputItem.MEINS);
                        if (!string.IsNullOrEmpty(outputItem.NETPR))
                            outputRow.SetValue("NETPR", outputItem.NETPR);
                        if (!string.IsNullOrEmpty(outputItem.EINDT))
                            outputRow.SetValue("EINDT", outputItem.EINDT);
                        if (!string.IsNullOrEmpty(outputItem.STATUS))
                            outputRow.SetValue("STATUS", outputItem.STATUS);
                        if (!string.IsNullOrEmpty(outputItem.MESSAGE))
                            outputRow.SetValue("MESSAGE", outputItem.MESSAGE);
                        
                        imOutputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = returnMessage });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "S", Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZMM_ART_MOD_PO_Request
    {
        public List<ZMM_PO_ART_Input> IM_INPUT { get; set; }
        public List<ZMM_PO_ART_Output> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_Input
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string EINDT { get; set; }
    }

    public class ZMM_PO_ART_Output
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string EINDT { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}