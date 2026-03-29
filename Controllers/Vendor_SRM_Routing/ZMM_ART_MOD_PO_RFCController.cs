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
    [RoutePrefix("api")]
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("ZMM_ART_MOD_PO_RFC")]
        public async Task<HttpResponseMessage> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
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
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT parameter
                if (request.IM_INPUT != null)
                {
                    IRfcStructure inputStruct = myfun.GetStructure("IM_INPUT");
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELN))
                        inputStruct.SetValue("EBELN", request.IM_INPUT.EBELN);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELP))
                        inputStruct.SetValue("EBELP", request.IM_INPUT.EBELP);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MATNR))
                        inputStruct.SetValue("MATNR", request.IM_INPUT.MATNR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.COLOR))
                        inputStruct.SetValue("COLOR", request.IM_INPUT.COLOR);
                    if (request.IM_INPUT.MENGE.HasValue)
                        inputStruct.SetValue("MENGE", request.IM_INPUT.MENGE.Value);
                    if (request.IM_INPUT.NETPR.HasValue)
                        inputStruct.SetValue("NETPR", request.IM_INPUT.NETPR.Value);
                }

                // Set IM_OUTPUT parameter
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        outputTable.Append();
                        if (!string.IsNullOrEmpty(item.EBELN))
                            outputTable.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP))
                            outputTable.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR))
                            outputTable.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            outputTable.SetValue("COLOR", item.COLOR);
                        if (item.MENGE.HasValue)
                            outputTable.SetValue("MENGE", item.MENGE.Value);
                        if (item.NETPR.HasValue)
                            outputTable.SetValue("NETPR", item.NETPR.Value);
                        if (!string.IsNullOrEmpty(item.STATUS))
                            outputTable.SetValue("STATUS", item.STATUS);
                        if (!string.IsNullOrEmpty(item.MESSAGE))
                            outputTable.SetValue("MESSAGE", item.MESSAGE);
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

    public class ZMM_ART_MOD_PO_RFCRequest
    {
        public ZMM_PO_ART_ST IM_INPUT { get; set; }
        public List<ZMM_PO_ART_OUT_TT> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
    }

    public class ZMM_PO_ART_OUT_TT
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}