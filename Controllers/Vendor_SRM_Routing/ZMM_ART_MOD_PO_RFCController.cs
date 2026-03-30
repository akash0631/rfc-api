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
    [Route("api/ZMM_ART_MOD_PO_RFC")]
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
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request body is null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT structure
                if (request.IM_INPUT != null)
                {
                    IRfcStructure imInput = myfun.GetStructure("IM_INPUT");
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELN))
                        imInput.SetValue("EBELN", request.IM_INPUT.EBELN);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EBELP))
                        imInput.SetValue("EBELP", request.IM_INPUT.EBELP);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MATNR))
                        imInput.SetValue("MATNR", request.IM_INPUT.MATNR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.COLOR))
                        imInput.SetValue("COLOR", request.IM_INPUT.COLOR);
                    if (request.IM_INPUT.MENGE.HasValue)
                        imInput.SetValue("MENGE", request.IM_INPUT.MENGE.Value);
                    if (request.IM_INPUT.NETPR.HasValue)
                        imInput.SetValue("NETPR", request.IM_INPUT.NETPR.Value);
                    if (request.IM_INPUT.AEDAT.HasValue)
                        imInput.SetValue("AEDAT", request.IM_INPUT.AEDAT.Value);
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutput = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        imOutput.Append();
                        IRfcStructure row = imOutput.CurrentRow;
                        if (!string.IsNullOrEmpty(item.EBELN))
                            row.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP))
                            row.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR))
                            row.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            row.SetValue("COLOR", item.COLOR);
                        if (item.MENGE.HasValue)
                            row.SetValue("MENGE", item.MENGE.Value);
                        if (item.NETPR.HasValue)
                            row.SetValue("NETPR", item.NETPR.Value);
                        if (!string.IsNullOrEmpty(item.STATUS))
                            row.SetValue("STATUS", item.STATUS);
                        if (!string.IsNullOrEmpty(item.MESSAGE))
                            row.SetValue("MESSAGE", item.MESSAGE);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string type = EX_RETURN.GetString("TYPE");
                string message = EX_RETURN.GetString("MESSAGE");

                if (type == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = message });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = type, Message = message });
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

    public class ZMM_ART_MOD_PO_RFCRequest
    {
        public ZMM_PO_ART_ST IM_INPUT { get; set; }
        public List<ZMM_PO_ART_OUT_IT> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public DateTime? AEDAT { get; set; }
    }

    public class ZMM_PO_ART_OUT_IT
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