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
        public HttpResponseMessage ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
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
                    if (!string.IsNullOrEmpty(request.IM_INPUT.WERKS))
                        imInput.SetValue("WERKS", request.IM_INPUT.WERKS);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.LGORT))
                        imInput.SetValue("LGORT", request.IM_INPUT.LGORT);
                    if (request.IM_INPUT.MENGE.HasValue)
                        imInput.SetValue("MENGE", request.IM_INPUT.MENGE.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MEINS))
                        imInput.SetValue("MEINS", request.IM_INPUT.MEINS);
                    if (request.IM_INPUT.NETPR.HasValue)
                        imInput.SetValue("NETPR", request.IM_INPUT.NETPR.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.WAERS))
                        imInput.SetValue("WAERS", request.IM_INPUT.WAERS);
                    if (request.IM_INPUT.EINDT.HasValue)
                        imInput.SetValue("EINDT", request.IM_INPUT.EINDT.Value);
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutput = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        imOutput.Append();
                        if (!string.IsNullOrEmpty(item.EBELN))
                            imOutput.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP))
                            imOutput.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR))
                            imOutput.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            imOutput.SetValue("COLOR", item.COLOR);
                        if (item.QUANTITY.HasValue)
                            imOutput.SetValue("QUANTITY", item.QUANTITY.Value);
                        if (!string.IsNullOrEmpty(item.UOM))
                            imOutput.SetValue("UOM", item.UOM);
                        if (item.UNIT_PRICE.HasValue)
                            imOutput.SetValue("UNIT_PRICE", item.UNIT_PRICE.Value);
                        if (!string.IsNullOrEmpty(item.CURRENCY))
                            imOutput.SetValue("CURRENCY", item.CURRENCY);
                        if (item.DELIVERY_DATE.HasValue)
                            imOutput.SetValue("DELIVERY_DATE", item.DELIVERY_DATE.Value);
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
        public string WERKS { get; set; }
        public string LGORT { get; set; }
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
        public decimal? QUANTITY { get; set; }
        public string UOM { get; set; }
        public decimal? UNIT_PRICE { get; set; }
        public string CURRENCY { get; set; }
        public DateTime? DELIVERY_DATE { get; set; }
    }
}