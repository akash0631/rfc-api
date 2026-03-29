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
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request cannot be null" });
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
                        if (!string.IsNullOrEmpty(item.EBELN)) inputTable.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP)) inputTable.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR)) inputTable.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR)) inputTable.SetValue("COLOR", item.COLOR);
                        if (!string.IsNullOrEmpty(item.MENGE)) inputTable.SetValue("MENGE", item.MENGE);
                        if (!string.IsNullOrEmpty(item.MEINS)) inputTable.SetValue("MEINS", item.MEINS);
                        if (!string.IsNullOrEmpty(item.EEIND)) inputTable.SetValue("EEIND", item.EEIND);
                        if (!string.IsNullOrEmpty(item.NETPR)) inputTable.SetValue("NETPR", item.NETPR);
                        if (!string.IsNullOrEmpty(item.PEINH)) inputTable.SetValue("PEINH", item.PEINH);
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        outputTable.Append();
                        if (!string.IsNullOrEmpty(item.EBELN)) outputTable.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP)) outputTable.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR)) outputTable.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR)) outputTable.SetValue("COLOR", item.COLOR);
                        if (!string.IsNullOrEmpty(item.MENGE)) outputTable.SetValue("MENGE", item.MENGE);
                        if (!string.IsNullOrEmpty(item.MEINS)) outputTable.SetValue("MEINS", item.MEINS);
                        if (!string.IsNullOrEmpty(item.EEIND)) outputTable.SetValue("EEIND", item.EEIND);
                        if (!string.IsNullOrEmpty(item.NETPR)) outputTable.SetValue("NETPR", item.NETPR);
                        if (!string.IsNullOrEmpty(item.PEINH)) outputTable.SetValue("PEINH", item.PEINH);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE")?.ToString() ?? "";
                string returnMessage = EX_RETURN.GetValue("MESSAGE")?.ToString() ?? "";

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

    public class ZMM_ART_MOD_PO_Request
    {
        public List<ZMM_PO_ART_Item> IM_INPUT { get; set; }
        public List<ZMM_PO_ART_OUT_Item> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_Item
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string EEIND { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
    }

    public class ZMM_PO_ART_OUT_Item
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string EEIND { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
    }
}