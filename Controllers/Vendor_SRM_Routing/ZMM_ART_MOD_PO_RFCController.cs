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
        public HttpResponseMessage ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_Request request)
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

                // Set IM_INPUT structure
                if (request.IM_INPUT != null)
                {
                    IRfcStructure imInputStruct = myfun.GetStructure("IM_INPUT");
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELN))
                        imInputStruct.SetValue("EBELN", request.IM_INPUT.EBELN);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELP))
                        imInputStruct.SetValue("EBELP", request.IM_INPUT.EBELP);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MATNR))
                        imInputStruct.SetValue("MATNR", request.IM_INPUT.MATNR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.COLOR))
                        imInputStruct.SetValue("COLOR", request.IM_INPUT.COLOR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MENGE))
                        imInputStruct.SetValue("MENGE", request.IM_INPUT.MENGE);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.NETPR))
                        imInputStruct.SetValue("NETPR", request.IM_INPUT.NETPR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EINDT))
                        imInputStruct.SetValue("EINDT", request.IM_INPUT.EINDT);
                }

                // Set IM_OUTPUT structure
                if (request.IM_OUTPUT != null)
                {
                    IRfcStructure imOutputStruct = myfun.GetStructure("IM_OUTPUT");
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.EBELN))
                        imOutputStruct.SetValue("EBELN", request.IM_OUTPUT.EBELN);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.EBELP))
                        imOutputStruct.SetValue("EBELP", request.IM_OUTPUT.EBELP);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.MATNR))
                        imOutputStruct.SetValue("MATNR", request.IM_OUTPUT.MATNR);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.COLOR))
                        imOutputStruct.SetValue("COLOR", request.IM_OUTPUT.COLOR);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.MENGE))
                        imOutputStruct.SetValue("MENGE", request.IM_OUTPUT.MENGE);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.NETPR))
                        imOutputStruct.SetValue("NETPR", request.IM_OUTPUT.NETPR);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.EINDT))
                        imOutputStruct.SetValue("EINDT", request.IM_OUTPUT.EINDT);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.STATUS))
                        imOutputStruct.SetValue("STATUS", request.IM_OUTPUT.STATUS);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.MESSAGE))
                        imOutputStruct.SetValue("MESSAGE", request.IM_OUTPUT.MESSAGE);
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = returnMessage });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = returnType, Message = returnMessage });
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
        public ZMM_PO_ART_ST IM_INPUT { get; set; }
        public ZMM_PO_ART_OUT_ST IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string EINDT { get; set; }
    }

    public class ZMM_PO_ART_OUT_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string EINDT { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}