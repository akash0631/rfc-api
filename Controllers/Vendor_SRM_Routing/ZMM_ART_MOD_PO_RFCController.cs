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
        public HttpResponseMessage ExecuteZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request cannot be null"
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT table
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    inputTable.Clear();
                    
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
                        if (inputItem.MENGE.HasValue)
                            inputRow.SetValue("MENGE", inputItem.MENGE.Value);
                        if (!string.IsNullOrEmpty(inputItem.MEINS))
                            inputRow.SetValue("MEINS", inputItem.MEINS);
                        if (inputItem.NETPR.HasValue)
                            inputRow.SetValue("NETPR", inputItem.NETPR.Value);
                        if (!string.IsNullOrEmpty(inputItem.WAERS))
                            inputRow.SetValue("WAERS", inputItem.WAERS);
                        if (inputItem.EINDT.HasValue)
                            inputRow.SetValue("EINDT", inputItem.EINDT.Value.ToString("yyyyMMdd"));
                        
                        inputTable.Append(inputRow);
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    outputTable.Clear();
                    
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
                        if (!string.IsNullOrEmpty(outputItem.STATUS))
                            outputRow.SetValue("STATUS", outputItem.STATUS);
                        if (!string.IsNullOrEmpty(outputItem.MESSAGE))
                            outputRow.SetValue("MESSAGE", outputItem.MESSAGE);
                        
                        outputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = returnMessage
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = returnType,
                    Message = returnMessage
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }

    public class ZMM_ART_MOD_PO_RFCRequest
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
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}