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
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request body is null or invalid" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT parameter
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    foreach (var item in request.IM_INPUT)
                    {
                        inputTable.Append();
                        if (!string.IsNullOrEmpty(item.EBELN))
                            inputTable.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP))
                            inputTable.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR))
                            inputTable.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.WERKS))
                            inputTable.SetValue("WERKS", item.WERKS);
                        if (!string.IsNullOrEmpty(item.LGORT))
                            inputTable.SetValue("LGORT", item.LGORT);
                        if (item.MENGE.HasValue)
                            inputTable.SetValue("MENGE", item.MENGE.Value);
                        if (item.NETPR.HasValue)
                            inputTable.SetValue("NETPR", item.NETPR.Value);
                        if (!string.IsNullOrEmpty(item.PEINH))
                            inputTable.SetValue("PEINH", item.PEINH);
                        if (!string.IsNullOrEmpty(item.WAERS))
                            inputTable.SetValue("WAERS", item.WAERS);
                        if (item.EINDT.HasValue)
                            inputTable.SetValue("EINDT", item.EINDT.Value);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            inputTable.SetValue("COLOR", item.COLOR);
                    }
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
                        if (!string.IsNullOrEmpty(item.WERKS))
                            outputTable.SetValue("WERKS", item.WERKS);
                        if (!string.IsNullOrEmpty(item.LGORT))
                            outputTable.SetValue("LGORT", item.LGORT);
                        if (item.MENGE.HasValue)
                            outputTable.SetValue("MENGE", item.MENGE.Value);
                        if (item.NETPR.HasValue)
                            outputTable.SetValue("NETPR", item.NETPR.Value);
                        if (!string.IsNullOrEmpty(item.PEINH))
                            outputTable.SetValue("PEINH", item.PEINH);
                        if (!string.IsNullOrEmpty(item.WAERS))
                            outputTable.SetValue("WAERS", item.WAERS);
                        if (item.EINDT.HasValue)
                            outputTable.SetValue("EINDT", item.EINDT.Value);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            outputTable.SetValue("COLOR", item.COLOR);
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
        public List<ZMM_PO_ART_Input> IM_INPUT { get; set; }
        public List<ZMM_PO_ART_Output> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_Input
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string PEINH { get; set; }
        public string WAERS { get; set; }
        public DateTime? EINDT { get; set; }
        public string COLOR { get; set; }
    }

    public class ZMM_PO_ART_Output
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string PEINH { get; set; }
        public string WAERS { get; set; }
        public DateTime? EINDT { get; set; }
        public string COLOR { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}