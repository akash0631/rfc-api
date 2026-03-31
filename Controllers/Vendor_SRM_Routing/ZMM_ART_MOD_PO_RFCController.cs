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
        public HttpResponseMessage ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
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
                    if (request.IM_INPUT.NETPR.HasValue)
                        imInput.SetValue("NETPR", request.IM_INPUT.NETPR.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.PEINH))
                        imInput.SetValue("PEINH", request.IM_INPUT.PEINH);
                    if (request.IM_INPUT.EEIND.HasValue)
                        imInput.SetValue("EEIND", request.IM_INPUT.EEIND.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EVERS))
                        imInput.SetValue("EVERS", request.IM_INPUT.EVERS);
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        IRfcStructure row = imOutputTable.Metadata.LineType.CreateStructure();
                        if (!string.IsNullOrEmpty(item.MATNR))
                            row.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            row.SetValue("COLOR", item.COLOR);
                        if (item.QUANTITY.HasValue)
                            row.SetValue("QUANTITY", item.QUANTITY.Value);
                        if (item.PRICE.HasValue)
                            row.SetValue("PRICE", item.PRICE.Value);
                        if (!string.IsNullOrEmpty(item.UOM))
                            row.SetValue("UOM", item.UOM);
                        if (item.DELIVERY_DATE.HasValue)
                            row.SetValue("DELIVERY_DATE", item.DELIVERY_DATE.Value);
                        imOutputTable.Append(row);
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

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = returnType, Message = returnMessage });
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
        public decimal? NETPR { get; set; }
        public string PEINH { get; set; }
        public DateTime? EEIND { get; set; }
        public string EVERS { get; set; }
    }

    public class ZMM_PO_ART_OUT_TT
    {
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? QUANTITY { get; set; }
        public decimal? PRICE { get; set; }
        public string UOM { get; set; }
        public DateTime? DELIVERY_DATE { get; set; }
    }
}