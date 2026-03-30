using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public async Task<HttpResponseMessage> ZMM_ART_MOD_PO_RFC(ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    imInputTable.Clear();
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = imInputTable.Metadata.LineType.CreateStructure();
                        if (!string.IsNullOrEmpty(inputItem.EBELN))
                            inputRow.SetValue("EBELN", inputItem.EBELN);
                        if (!string.IsNullOrEmpty(inputItem.EBELP))
                            inputRow.SetValue("EBELP", inputItem.EBELP);
                        if (!string.IsNullOrEmpty(inputItem.MATNR))
                            inputRow.SetValue("MATNR", inputItem.MATNR);
                        if (!string.IsNullOrEmpty(inputItem.WERKS))
                            inputRow.SetValue("WERKS", inputItem.WERKS);
                        if (!string.IsNullOrEmpty(inputItem.LGORT))
                            inputRow.SetValue("LGORT", inputItem.LGORT);
                        if (inputItem.MENGE.HasValue)
                            inputRow.SetValue("MENGE", inputItem.MENGE.Value);
                        if (inputItem.NETPR.HasValue)
                            inputRow.SetValue("NETPR", inputItem.NETPR.Value);
                        if (!string.IsNullOrEmpty(inputItem.PEINH))
                            inputRow.SetValue("PEINH", inputItem.PEINH);
                        if (!string.IsNullOrEmpty(inputItem.MEINS))
                            inputRow.SetValue("MEINS", inputItem.MEINS);
                        if (inputItem.EINDT.HasValue)
                            inputRow.SetValue("EINDT", inputItem.EINDT.Value.ToString("yyyyMMdd"));
                        if (!string.IsNullOrEmpty(inputItem.UEBTO))
                            inputRow.SetValue("UEBTO", inputItem.UEBTO);
                        if (!string.IsNullOrEmpty(inputItem.UEBTK))
                            inputRow.SetValue("UEBTK", inputItem.UEBTK);
                        if (!string.IsNullOrEmpty(inputItem.UNTTO))
                            inputRow.SetValue("UNTTO", inputItem.UNTTO);
                        if (!string.IsNullOrEmpty(inputItem.BSTAE))
                            inputRow.SetValue("BSTAE", inputItem.BSTAE);
                        imInputTable.Append(inputRow);
                    }
                }

                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    imOutputTable.Clear();
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = imOutputTable.Metadata.LineType.CreateStructure();
                        if (!string.IsNullOrEmpty(outputItem.EBELN))
                            outputRow.SetValue("EBELN", outputItem.EBELN);
                        if (!string.IsNullOrEmpty(outputItem.EBELP))
                            outputRow.SetValue("EBELP", outputItem.EBELP);
                        if (!string.IsNullOrEmpty(outputItem.MESSAGE))
                            outputRow.SetValue("MESSAGE", outputItem.MESSAGE);
                        if (!string.IsNullOrEmpty(outputItem.STATUS))
                            outputRow.SetValue("STATUS", outputItem.STATUS);
                        imOutputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string status = EX_RETURN.GetValue("TYPE").ToString();
                string message = EX_RETURN.GetValue("MESSAGE").ToString();

                if (status == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = message });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = status, Message = message });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (CommunicationException ex)
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
        public List<IM_INPUT_Item> IM_INPUT { get; set; }
        public List<IM_OUTPUT_Item> IM_OUTPUT { get; set; }
    }

    public class IM_INPUT_Item
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string PEINH { get; set; }
        public string MEINS { get; set; }
        public DateTime? EINDT { get; set; }
        public string UEBTO { get; set; }
        public string UEBTK { get; set; }
        public string UNTTO { get; set; }
        public string BSTAE { get; set; }
    }

    public class IM_OUTPUT_Item
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MESSAGE { get; set; }
        public string STATUS { get; set; }
    }
}