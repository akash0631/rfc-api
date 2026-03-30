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
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request cannot be null"
                    });
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
                        var row = imInputTable.CurrentRow;
                        
                        if (!string.IsNullOrEmpty(inputItem.EBELN))
                            row.SetValue("EBELN", inputItem.EBELN);
                        if (!string.IsNullOrEmpty(inputItem.EBELP))
                            row.SetValue("EBELP", inputItem.EBELP);
                        if (!string.IsNullOrEmpty(inputItem.MATNR))
                            row.SetValue("MATNR", inputItem.MATNR);
                        if (!string.IsNullOrEmpty(inputItem.COLOR))
                            row.SetValue("COLOR", inputItem.COLOR);
                        if (inputItem.MENGE.HasValue)
                            row.SetValue("MENGE", inputItem.MENGE.Value);
                        if (inputItem.NETPR.HasValue)
                            row.SetValue("NETPR", inputItem.NETPR.Value);
                        if (!string.IsNullOrEmpty(inputItem.MEINS))
                            row.SetValue("MEINS", inputItem.MEINS);
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        imOutputTable.Append();
                        var row = imOutputTable.CurrentRow;
                        
                        if (!string.IsNullOrEmpty(outputItem.EBELN))
                            row.SetValue("EBELN", outputItem.EBELN);
                        if (!string.IsNullOrEmpty(outputItem.EBELP))
                            row.SetValue("EBELP", outputItem.EBELP);
                        if (!string.IsNullOrEmpty(outputItem.MATNR))
                            row.SetValue("MATNR", outputItem.MATNR);
                        if (!string.IsNullOrEmpty(outputItem.COLOR))
                            row.SetValue("COLOR", outputItem.COLOR);
                        if (!string.IsNullOrEmpty(outputItem.STATUS))
                            row.SetValue("STATUS", outputItem.STATUS);
                        if (!string.IsNullOrEmpty(outputItem.MESSAGE))
                            row.SetValue("MESSAGE", outputItem.MESSAGE);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = returnMessage
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = returnType,
                    Message = returnMessage
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Status = "E",
                    Message = ex.Message
                });
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
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string MEINS { get; set; }
    }

    public class ZMM_PO_ART_Output
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}