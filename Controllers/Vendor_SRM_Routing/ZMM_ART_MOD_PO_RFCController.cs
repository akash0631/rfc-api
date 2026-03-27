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
        public HttpResponseMessage ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
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

                // Set IM_INPUT structure
                if (request.IM_INPUT != null)
                {
                    IRfcStructure inputStruct = myfun.GetStructure("IM_INPUT");
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELN))
                        inputStruct.SetValue("EBELN", request.IM_INPUT.EBELN);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELP))
                        inputStruct.SetValue("EBELP", request.IM_INPUT.EBELP);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MATNR))
                        inputStruct.SetValue("MATNR", request.IM_INPUT.MATNR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.WERKS))
                        inputStruct.SetValue("WERKS", request.IM_INPUT.WERKS);
                    if (request.IM_INPUT.MENGE.HasValue)
                        inputStruct.SetValue("MENGE", request.IM_INPUT.MENGE.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MEINS))
                        inputStruct.SetValue("MEINS", request.IM_INPUT.MEINS);
                    if (request.IM_INPUT.NETPR.HasValue)
                        inputStruct.SetValue("NETPR", request.IM_INPUT.NETPR.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.WAERS))
                        inputStruct.SetValue("WAERS", request.IM_INPUT.WAERS);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EINDT))
                        inputStruct.SetValue("EINDT", request.IM_INPUT.EINDT);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.LGORT))
                        inputStruct.SetValue("LGORT", request.IM_INPUT.LGORT);
                }

                // Set IM_OUTPUT structure
                if (request.IM_OUTPUT != null)
                {
                    IRfcStructure outputStruct = myfun.GetStructure("IM_OUTPUT");
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.EBELN))
                        outputStruct.SetValue("EBELN", request.IM_OUTPUT.EBELN);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.EBELP))
                        outputStruct.SetValue("EBELP", request.IM_OUTPUT.EBELP);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.MATNR))
                        outputStruct.SetValue("MATNR", request.IM_OUTPUT.MATNR);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.WERKS))
                        outputStruct.SetValue("WERKS", request.IM_OUTPUT.WERKS);
                    if (request.IM_OUTPUT.MENGE.HasValue)
                        outputStruct.SetValue("MENGE", request.IM_OUTPUT.MENGE.Value);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.MEINS))
                        outputStruct.SetValue("MEINS", request.IM_OUTPUT.MEINS);
                    if (request.IM_OUTPUT.NETPR.HasValue)
                        outputStruct.SetValue("NETPR", request.IM_OUTPUT.NETPR.Value);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.WAERS))
                        outputStruct.SetValue("WAERS", request.IM_OUTPUT.WAERS);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.EINDT))
                        outputStruct.SetValue("EINDT", request.IM_OUTPUT.EINDT);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.LGORT))
                        outputStruct.SetValue("LGORT", request.IM_OUTPUT.LGORT);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.LOEKZ))
                        outputStruct.SetValue("LOEKZ", request.IM_OUTPUT.LOEKZ);
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE")?.ToString() ?? "";
                string returnMessage = EX_RETURN.GetValue("MESSAGE")?.ToString() ?? "";

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
                    Status = returnType == "S" ? "S" : "I",
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
        public ZMM_PO_ART_ST IM_INPUT { get; set; }
        public ZMM_PO_ART_OUT_ST IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public decimal? MENGE { get; set; }
        public string MEINS { get; set; }
        public decimal? NETPR { get; set; }
        public string WAERS { get; set; }
        public string EINDT { get; set; }
        public string LGORT { get; set; }
    }

    public class ZMM_PO_ART_OUT_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public decimal? MENGE { get; set; }
        public string MEINS { get; set; }
        public decimal? NETPR { get; set; }
        public string WAERS { get; set; }
        public string EINDT { get; set; }
        public string LGORT { get; set; }
        public string LOEKZ { get; set; }
    }
}