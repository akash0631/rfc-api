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
        public async Task<HttpResponseMessage> ZMM_ART_MOD_PO_RFC(ZMM_ART_MOD_PO_RFC_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT table parameter
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = inputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(inputItem.EBELN))
                            inputRow.SetValue("EBELN", inputItem.EBELN);
                        if (!string.IsNullOrEmpty(inputItem.EBELP))
                            inputRow.SetValue("EBELP", inputItem.EBELP);
                        if (!string.IsNullOrEmpty(inputItem.MATNR))
                            inputRow.SetValue("MATNR", inputItem.MATNR);
                        if (!string.IsNullOrEmpty(inputItem.COLOR))
                            inputRow.SetValue("COLOR", inputItem.COLOR);
                        if (inputItem.MENGE != null)
                            inputRow.SetValue("MENGE", inputItem.MENGE);
                        if (!string.IsNullOrEmpty(inputItem.MEINS))
                            inputRow.SetValue("MEINS", inputItem.MEINS);
                        if (inputItem.NETPR != null)
                            inputRow.SetValue("NETPR", inputItem.NETPR);
                        if (!string.IsNullOrEmpty(inputItem.WAERS))
                            inputRow.SetValue("WAERS", inputItem.WAERS);
                        if (inputItem.EINDT != null && inputItem.EINDT != DateTime.MinValue)
                            inputRow.SetValue("EINDT", inputItem.EINDT);
                        
                        inputTable.Append(inputRow);
                    }
                }

                // Set IM_OUTPUT table parameter
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = outputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(outputItem.EBELN))
                            outputRow.SetValue("EBELN", outputItem.EBELN);
                        if (!string.IsNullOrEmpty(outputItem.EBELP))
                            outputRow.SetValue("EBELP", outputItem.EBELP);
                        if (!string.IsNullOrEmpty(outputItem.MATNR))
                            outputRow.SetValue("MATNR", outputItem.MATNR);
                        if (!string.IsNullOrEmpty(outputItem.COLOR))
                            outputRow.SetValue("COLOR", outputItem.COLOR);
                        if (outputItem.MENGE != null)
                            outputRow.SetValue("MENGE", outputItem.MENGE);
                        if (!string.IsNullOrEmpty(outputItem.MEINS))
                            outputRow.SetValue("MEINS", outputItem.MEINS);
                        if (outputItem.NETPR != null)
                            outputRow.SetValue("NETPR", outputItem.NETPR);
                        if (!string.IsNullOrEmpty(outputItem.WAERS))
                            outputRow.SetValue("WAERS", outputItem.WAERS);
                        if (outputItem.EINDT != null && outputItem.EINDT != DateTime.MinValue)
                            outputRow.SetValue("EINDT", outputItem.EINDT);
                        if (!string.IsNullOrEmpty(outputItem.LIFNR))
                            outputRow.SetValue("LIFNR", outputItem.LIFNR);
                        if (!string.IsNullOrEmpty(outputItem.WERKS))
                            outputRow.SetValue("WERKS", outputItem.WERKS);
                        
                        outputTable.Append(outputRow);
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

    public class ZMM_ART_MOD_PO_RFC_Request
    {
        public List<ZMM_PO_ART_TT> IM_INPUT { get; set; }
        public List<ZMM_PO_ART_OUT_TT> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_TT
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? MENGE { get; set; }
        public string MEINS { get; set; }
        public decimal? NETPR { get; set; }
        public string WAERS { get; set; }
        public DateTime? EINDT { get; set; }
    }

    public class ZMM_PO_ART_OUT_TT
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? MENGE { get; set; }
        public string MEINS { get; set; }
        public decimal? NETPR { get; set; }
        public string WAERS { get; set; }
        public DateTime? EINDT { get; set; }
        public string LIFNR { get; set; }
        public string WERKS { get; set; }
    }
}