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
        public async Task<HttpResponseMessage> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request body is null or empty" });
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
                        IRfcStructure row = inputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(item.EBELN)) row.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP)) row.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR)) row.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR)) row.SetValue("COLOR", item.COLOR);
                        if (!string.IsNullOrEmpty(item.MENGE)) row.SetValue("MENGE", item.MENGE);
                        if (!string.IsNullOrEmpty(item.NETPR)) row.SetValue("NETPR", item.NETPR);
                        if (!string.IsNullOrEmpty(item.WAERS)) row.SetValue("WAERS", item.WAERS);
                        
                        inputTable.Append(row);
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        IRfcStructure row = outputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(item.EBELN)) row.SetValue("EBELN", item.EBELN);
                        if (!string.IsNullOrEmpty(item.EBELP)) row.SetValue("EBELP", item.EBELP);
                        if (!string.IsNullOrEmpty(item.MATNR)) row.SetValue("MATNR", item.MATNR);
                        if (!string.IsNullOrEmpty(item.COLOR)) row.SetValue("COLOR", item.COLOR);
                        if (!string.IsNullOrEmpty(item.MENGE)) row.SetValue("MENGE", item.MENGE);
                        if (!string.IsNullOrEmpty(item.NETPR)) row.SetValue("NETPR", item.NETPR);
                        if (!string.IsNullOrEmpty(item.WAERS)) row.SetValue("WAERS", item.WAERS);
                        if (!string.IsNullOrEmpty(item.STATUS)) row.SetValue("STATUS", item.STATUS);
                        if (!string.IsNullOrEmpty(item.MESSAGE)) row.SetValue("MESSAGE", item.MESSAGE);
                        
                        outputTable.Append(row);
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
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string WAERS { get; set; }
    }

    public class ZMM_PO_ART_Output
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string WAERS { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}