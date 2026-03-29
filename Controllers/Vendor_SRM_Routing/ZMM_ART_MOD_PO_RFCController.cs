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
    [RoutePrefix("api")]
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("ZMM_ART_MOD_PO_RFC")]
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

                // Set IM_INPUT table
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        imInputTable.Append();
                        imInputTable.SetValue("MANDT", inputItem.MANDT ?? "");
                        imInputTable.SetValue("EBELN", inputItem.EBELN ?? "");
                        imInputTable.SetValue("EBELP", inputItem.EBELP ?? "");
                        imInputTable.SetValue("MATNR", inputItem.MATNR ?? "");
                        imInputTable.SetValue("TXZ01", inputItem.TXZ01 ?? "");
                        imInputTable.SetValue("MENGE", inputItem.MENGE ?? "");
                        imInputTable.SetValue("MEINS", inputItem.MEINS ?? "");
                        imInputTable.SetValue("NETPR", inputItem.NETPR ?? "");
                        imInputTable.SetValue("PEINH", inputItem.PEINH ?? "");
                        imInputTable.SetValue("BPRME", inputItem.BPRME ?? "");
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        imOutputTable.Append();
                        imOutputTable.SetValue("MANDT", outputItem.MANDT ?? "");
                        imOutputTable.SetValue("EBELN", outputItem.EBELN ?? "");
                        imOutputTable.SetValue("EBELP", outputItem.EBELP ?? "");
                        imOutputTable.SetValue("MATNR", outputItem.MATNR ?? "");
                        imOutputTable.SetValue("TXZ01", outputItem.TXZ01 ?? "");
                        imOutputTable.SetValue("MENGE", outputItem.MENGE ?? "");
                        imOutputTable.SetValue("MEINS", outputItem.MEINS ?? "");
                        imOutputTable.SetValue("NETPR", outputItem.NETPR ?? "");
                        imOutputTable.SetValue("PEINH", outputItem.PEINH ?? "");
                        imOutputTable.SetValue("BPRME", outputItem.BPRME ?? "");
                        imOutputTable.SetValue("MESSAGE", outputItem.MESSAGE ?? "");
                        imOutputTable.SetValue("TYPE", outputItem.TYPE ?? "");
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
        public string TXZ01 { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
        public string BPRME { get; set; }
    }

    public class ZMM_PO_ART_OUT_TT
    {
        public string MANDT { get; set; }
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string TXZ01 { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
        public string BPRME { get; set; }
        public string MESSAGE { get; set; }
        public string TYPE { get; set; }
    }
}