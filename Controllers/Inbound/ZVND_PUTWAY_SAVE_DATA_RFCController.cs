using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    /// <summary>
    /// RFC: ZVND_PUTWAY_SAVE_DATA_RFC
    /// Purpose: Save putway data to SAP after BIN and Palette validation.
    /// IMPORT: IM_USER (WWWOBJID)
    /// TABLE:  IT_DATA (ZTT_PUTWAY_SAVE) - putway records
    ///         Table fields: PLANT (WERKS_D), BIN (LGPLA), PALETTE (ZZPALETTE),
    ///                       EXT_HU (ZEXT_HU), PO_NO (EBELN), BILL_NO (ZBILL_NO)
    /// EXPORT: EX_RETURN (BAPIRET2)
    /// </summary>
    public class ZVND_PUTWAY_SAVE_DATA_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZVND_PUTWAY_SAVE_DATA_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;

                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_PUTWAY_SAVE_DATA_RFC");

                    myfun.SetValue("IM_USER", request.IM_USER);

                    // Populate IT_DATA table (ZTT_PUTWAY_SAVE)
                    IRfcTable itData = myfun.GetTable("IT_DATA");
                    if (request.IT_DATA != null)
                    {
                        foreach (var row in request.IT_DATA)
                        {
                            itData.Append();
                            itData.SetValue("PLANT",   row.PLANT);
                            itData.SetValue("BIN",     row.BIN);
                            itData.SetValue("PALETTE", row.PALETTE);
                            itData.SetValue("EXT_HU",  row.EXT_HU);
                            itData.SetValue("PO_NO",   row.PO_NO);
                            itData.SetValue("BILL_NO", row.BILL_NO);
                        }
                    }

                    myfun.Invoke(dest);

                    IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                    string sapType    = exReturn.GetValue("TYPE").ToString();
                    string sapMessage = exReturn.GetValue("MESSAGE").ToString();

                    if (sapType == "E")
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = false,
                            Message = sapMessage
                        });
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status  = true,
                        Message = sapMessage
                    });
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status  = false,
                        Message = ex.Message
                    });
                }
            });
        }
    }

    public class ZVND_PUTWAY_SAVE_DATA_RFCRequest
    {
        public string IM_USER { get; set; }                              // WWWOBJID - logged-in user
        public List<ZVND_PUTWAY_SAVE_DataRow> IT_DATA { get; set; }     // ZTT_PUTWAY_SAVE - putway records
    }

    public class ZVND_PUTWAY_SAVE_DataRow
    {
        public string PLANT   { get; set; }   // WERKS_D   - plant/DC
        public string BIN     { get; set; }   // LGPLA     - bin location
        public string PALETTE { get; set; }   // ZZPALETTE - palette number
        public string EXT_HU  { get; set; }   // ZEXT_HU   - external HU number
        public string PO_NO   { get; set; }   // EBELN     - purchase order number
        public string BILL_NO { get; set; }   // ZBILL_NO  - bill/invoice number
    }
}
