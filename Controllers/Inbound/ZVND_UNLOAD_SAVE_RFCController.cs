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
    /// RFC: ZVND_UNLOAD_SAVE_RFC
    /// Purpose: Save unloading data to SAP after HU and Palette validation.
    /// IMPORT: IM_USER (WWWOBJID), IM_PARMS (ZTT_UNLOAD_SAVE) - table
    ///         Table fields: PLANT (WERKS_D), VEHICLE (ZVEH), EXT_HU (ZEXT_HU),
    ///                       PALETTE (ZZPALETTE), PO_NO (EBELN), BILL_NO (ZBILL_NO)
    /// EXPORT: EX_RETURN (BAPIRET2)
    /// </summary>
    public class ZVND_UNLOAD_SAVE_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZVND_UNLOAD_SAVE_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;

                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_UNLOAD_SAVE_RFC");

                    myfun.SetValue("IM_USER", request.IM_USER);

                    // Populate IM_PARMS table (ZTT_UNLOAD_SAVE)
                    IRfcTable imParms = myfun.GetTable("IM_PARMS");
                    if (request.IM_PARMS != null)
                    {
                        foreach (var row in request.IM_PARMS)
                        {
                            imParms.Append();
                            imParms.SetValue("PLANT",   row.PLANT);
                            imParms.SetValue("VEHICLE", row.VEHICLE);
                            imParms.SetValue("EXT_HU",  row.EXT_HU);
                            imParms.SetValue("PALETTE", row.PALETTE);
                            imParms.SetValue("PO_NO",   row.PO_NO);
                            imParms.SetValue("BILL_NO", row.BILL_NO);
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

    public class ZVND_UNLOAD_SAVE_RFCRequest
    {
        public string IM_USER { get; set; }                            // WWWOBJID - logged-in user
        public List<ZVND_UNLOAD_SAVE_ParmsRow> IM_PARMS { get; set; } // ZTT_UNLOAD_SAVE - save records
    }

    public class ZVND_UNLOAD_SAVE_ParmsRow
    {
        public string PLANT   { get; set; }   // WERKS_D    - plant/DC
        public string VEHICLE { get; set; }   // ZVEH       - vehicle number
        public string EXT_HU  { get; set; }   // ZEXT_HU    - external HU number
        public string PALETTE { get; set; }   // ZZPALETTE  - palette number
        public string PO_NO   { get; set; }   // EBELN      - purchase order number
        public string BILL_NO { get; set; }   // ZBILL_NO   - bill/invoice number
    }
}
