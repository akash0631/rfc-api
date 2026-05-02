using SAP.Middleware.Connector;
using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.HU_Creation
{
    /// <summary>
    /// HU Stock Reversal RFC.
    /// Reverses/moves a Handling Unit (HU) in SAP WM — moves HU from one storage type to another.
    /// Import: IV_WERKS (plant, e.g. B03), IV_HU (external HU ID), IV_LGTYP (storage type, e.g. V11).
    /// Export: ES_RETURN (BAPIRET2 structure — pass by reference).
    /// RFC: ZWM_HU_STOCK_REV_RFC | Function group: ZWM_HU_STOCK_FG
    /// Test: IV_WERKS=B03, IV_HU=100000776, IV_LGTYP=V11
    /// </summary>
    [RoutePrefix("api")]
    public class ZWM_HU_STOCK_REV_RFCController : BaseController
    {
        [HttpPost, Route("ZWM_HU_STOCK_REV_RFC")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] HuStockRevRequest request)
        {
            try
            {
                if (request == null)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Status = "E", Message = "Request body required: IV_WERKS, IV_HU, IV_LGTYP." });

                if (string.IsNullOrWhiteSpace(request.IV_WERKS) ||
                    string.IsNullOrWhiteSpace(request.IV_HU)    ||
                    string.IsNullOrWhiteSpace(request.IV_LGTYP))
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Status = "E", Message = "IV_WERKS, IV_HU and IV_LGTYP are all required." });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction("ZWM_HU_STOCK_REV_RFC");

                // Import parameters: RULE 2 — use exact component names from RFC spec
                rfcFunction.SetValue("IV_WERKS", request.IV_WERKS);  // Plant (WERKS_D)
                rfcFunction.SetValue("IV_HU",    request.IV_HU);     // External HU ID (EXIDV)
                rfcFunction.SetValue("IV_LGTYP", request.IV_LGTYP);  // Storage type (LGTYP)

                rfcFunction.Invoke(dest);

                // RULE 1: ES_RETURN is BAPIRET2 STRUCTURE (pass by reference) → GetStructure
                IRfcStructure esReturn = rfcFunction.GetStructure("ES_RETURN");
                string retType    = esReturn.GetString("TYPE");
                string retMessage = esReturn.GetString("MESSAGE");

                return Request.CreateResponse(
                    retType == "E" ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
                    new
                    {
                        Status  = retType,
                        Message = retMessage,
                        Data = new
                        {
                            Plant       = request.IV_WERKS,
                            HU          = request.IV_HU,
                            StorageType = request.IV_LGTYP,
                            ES_RETURN = new
                            {
                                TYPE    = retType,
                                ID      = esReturn.GetString("ID"),
                                NUMBER  = esReturn.GetString("NUMBER"),
                                MESSAGE = retMessage
                            }
                        }
                    });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class HuStockRevRequest
    {
        /// <summary>Plant code e.g. B03</summary>
        public string IV_WERKS { get; set; }
        /// <summary>External Handling Unit ID e.g. 100000776</summary>
        public string IV_HU    { get; set; }
        /// <summary>Storage type e.g. V11</summary>
        public string IV_LGTYP { get; set; }
    }
}
