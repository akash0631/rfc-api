using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.GateEntry
{
    /// <summary>
    /// Gate Entry RFC.
    /// Fetches gate entry records for a given purchasing document (PO number).
    /// Returns lot quantities, pending lots, agent codes, plant, invoice numbers.
    /// RFC: ZWM_GATE_ENTRY01_RFC | Function group: ZWM_GATE_FG
    /// Import: IM_EBELN (PO number). Export: EX_RETURN (BAPIRET2), ET_DATA (ZMGATE_ENTRY01IT).
    /// </summary>
    [RoutePrefix("api")]
    public class ZWM_GATE_ENTRY01_RFCController : BaseController
    {
        [HttpPost, Route("ZWM_GATE_ENTRY01_RFC")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] GateEntryRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.IM_EBELN))
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Status = "E", Message = "IM_EBELN (PO number) is required." });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction("ZWM_GATE_ENTRY01_RFC");

                // Import: IM_EBELN — purchasing document number (CHAR 10)
                rfcFunction.SetValue("IM_EBELN", request.IM_EBELN);

                rfcFunction.Invoke(dest);

                // RULE 1: EX_RETURN is BAPIRET2 STRUCTURE → GetStructure (not GetTable)
                IRfcStructure exReturn = rfcFunction.GetStructure("EX_RETURN");

                // Output table: ET_DATA (ZMGATE_ENTRY01IT)
                IRfcTable etData = rfcFunction.GetTable("ET_DATA");
                var rows = new List<object>();
                foreach (IRfcStructure row in etData)
                {
                    rows.Add(new
                    {
                        EDOCNO          = row.GetString("EDOCNO"),
                        LOT_QTY         = row.GetString("LOT_QTY"),
                        PENDING_LOT_QTY = row.GetString("PENDING_LOT_QTY"),
                        LOT_AG          = row.GetString("LOT_AG"),
                        PRO_AG          = row.GetString("PRO_AG"),
                        PONO            = row.GetString("PONO"),
                        PLANT           = row.GetString("PLANT"),
                        INVNO           = row.GetString("INVNO"),
                        INVNO6          = row.GetString("INVNO6")
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status      = exReturn.GetString("TYPE"),
                    Message     = exReturn.GetString("MESSAGE"),
                    RecordCount = rows.Count,
                    PO          = request.IM_EBELN,
                    Data = new { ET_DATA = rows }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class GateEntryRequest
    {
        /// <summary>Purchasing Document Number (PO) — CHAR 10</summary>
        public string IM_EBELN { get; set; }
    }
}
