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
        public async Task<HttpResponseMessage> ModifyPOArticleColor([FromBody] ZMM_ART_MOD_PO_Request request)
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
                IRfcStructure imInput = myfun.GetStructure("IM_INPUT");
                if (request.IM_INPUT != null)
                {
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
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MEINS))
                        imInput.SetValue("MEINS", request.IM_INPUT.MEINS);
                    if (request.IM_INPUT.EINDT.HasValue)
                        imInput.SetValue("EINDT", request.IM_INPUT.EINDT.Value.ToString("yyyyMMdd"));
                }

                // Set IM_OUTPUT table
                IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    foreach (var item in request.IM_OUTPUT)
                    {
                        imOutputTable.Append();
                        IRfcStructure row = imOutputTable.CurrentRow;
                        
                        if (!string.IsNullOrEmpty(item.EBELN))
                            row.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP))
                            row.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR))
                            row.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.WERKS))
                            row.SetValue("WERKS", item.WERKS);
                        if (!string.IsNullOrEmpty(item.LGORT))
                            row.SetValue("LGORT", item.LGORT);
                        if (item.MENGE.HasValue)
                            row.SetValue("MENGE", item.MENGE.Value);
                        if (item.NETPR.HasValue)
                            row.SetValue("NETPR", item.NETPR.Value);
                        if (!string.IsNullOrEmpty(item.MEINS))
                            row.SetValue("MEINS", item.MEINS);
                        if (item.EINDT.HasValue)
                            row.SetValue("EINDT", item.EINDT.Value.ToString("yyyyMMdd"));
                        if (!string.IsNullOrEmpty(item.COLOR))
                            row.SetValue("COLOR", item.COLOR);
                        if (!string.IsNullOrEmpty(item.SIZE1))
                            row.SetValue("SIZE1", item.SIZE1);
                        if (!string.IsNullOrEmpty(item.SEASON))
                            row.SetValue("SEASON", item.SEASON);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = returnMessage });
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
        public string MEINS { get; set; }
        public DateTime? EINDT { get; set; }
    }

    public class ZMM_PO_ART_OUT_TT
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string MEINS { get; set; }
        public DateTime? EINDT { get; set; }
        public string COLOR { get; set; }
        public string SIZE1 { get; set; }
        public string SEASON { get; set; }
    }
}