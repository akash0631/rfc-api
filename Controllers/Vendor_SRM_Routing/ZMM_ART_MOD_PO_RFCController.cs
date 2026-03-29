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

                // Set IM_INPUT table
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    foreach (var item in request.IM_INPUT)
                    {
                        inputTable.Append();
                        IRfcStructure inputRow = inputTable.CurrentRow;
                        
                        if (!string.IsNullOrEmpty(item.MANDT))
                            inputRow.SetValue("MANDT", item.MANDT);
                        if (!string.IsNullOrEmpty(item.EBELN))
                            inputRow.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP))
                            inputRow.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR))
                            inputRow.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.WERKS))
                            inputRow.SetValue("WERKS", item.WERKS);
                        if (!string.IsNullOrEmpty(item.LGORT))
                            inputRow.SetValue("LGORT", item.LGORT);
                        if (!string.IsNullOrEmpty(item.MENGE))
                            inputRow.SetValue("MENGE", item.MENGE);
                        if (!string.IsNullOrEmpty(item.MEINS))
                            inputRow.SetValue("MEINS", item.MEINS);
                        if (!string.IsNullOrEmpty(item.NETPR))
                            inputRow.SetValue("NETPR", item.NETPR);
                        if (!string.IsNullOrEmpty(item.PEINH))
                            inputRow.SetValue("PEINH", item.PEINH);
                        if (!string.IsNullOrEmpty(item.NETWR))
                            inputRow.SetValue("NETWR", item.NETWR);
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        outputTable.Append();
                        IRfcStructure outputRow = outputTable.CurrentRow;
                        
                        if (!string.IsNullOrEmpty(item.MANDT))
                            outputRow.SetValue("MANDT", item.MANDT);
                        if (!string.IsNullOrEmpty(item.EBELN))
                            outputRow.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP))
                            outputRow.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR))
                            outputRow.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.WERKS))
                            outputRow.SetValue("WERKS", item.WERKS);
                        if (!string.IsNullOrEmpty(item.LGORT))
                            outputRow.SetValue("LGORT", item.LGORT);
                        if (!string.IsNullOrEmpty(item.MENGE))
                            outputRow.SetValue("MENGE", item.MENGE);
                        if (!string.IsNullOrEmpty(item.MEINS))
                            outputRow.SetValue("MEINS", item.MEINS);
                        if (!string.IsNullOrEmpty(item.NETPR))
                            outputRow.SetValue("NETPR", item.NETPR);
                        if (!string.IsNullOrEmpty(item.PEINH))
                            outputRow.SetValue("PEINH", item.PEINH);
                        if (!string.IsNullOrEmpty(item.NETWR))
                            outputRow.SetValue("NETWR", item.NETWR);
                        if (!string.IsNullOrEmpty(item.STATUS))
                            outputRow.SetValue("STATUS", item.STATUS);
                        if (!string.IsNullOrEmpty(item.MESSAGE))
                            outputRow.SetValue("MESSAGE", item.MESSAGE);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE").ToString();
                string returnMessage = EX_RETURN.GetValue("MESSAGE").ToString();

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = returnMessage });
                }

                var response = new
                {
                    Status = returnType,
                    Message = returnMessage
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
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
        public List<ZMM_PO_ART_TT> IM_INPUT { get; set; }
        public List<ZMM_PO_ART_OUT_TT> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_TT
    {
        public string MANDT { get; set; }
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
        public string NETWR { get; set; }
    }

    public class ZMM_PO_ART_OUT_TT
    {
        public string MANDT { get; set; }
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
        public string NETWR { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}