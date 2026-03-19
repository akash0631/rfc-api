using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    /// <summary>
    /// API Controller for ZVND_PUTWAY_SAVE_DATA_RFC
    /// Technical Details: Putway save data RFC.
    /// Accepts IM_USER (Import) and IT_DATA table (ZTT_PUTWAY_SAVE) as input.
    /// Returns EX_RETURN (BAPIRET2).
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
                    if (request.IM_USER != null)
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = rfcrep.CreateFunction("ZVND_PUTWAY_SAVE_DATA_RFC");

                        // --- IMPORT parameter ---
                        myfun.SetValue("IM_USER", request.IM_USER);

                        // --- TABLE parameter: IT_DATA (ZTT_PUTWAY_SAVE) ---
                        if (request.IT_DATA != null && request.IT_DATA.Count > 0)
                        {
                            IRfcTable itData = myfun.GetTable("IT_DATA");
                            foreach (var row in request.IT_DATA)
                            {
                                itData.Append();
                                foreach (var kvp in row)
                                {
                                    try { itData.SetValue(kvp.Key, kvp.Value); }
                                    catch { /* skip unknown / unsettable fields */ }
                                }
                            }
                        }

                        myfun.Invoke(dest);

                        IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                        string SAP_TYPE = EX_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = EX_RETURN.GetValue("MESSAGE").ToString();

                        if (SAP_TYPE == "E")
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = false,
                                Message = SAP_Message
                            });
                        }
                        else
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = true,
                                Message = SAP_Message
                            });
                        }
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = false,
                            Message = "Request Not Valid"
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status = false,
                        Message = ex.Message
                    });
                }
            });
        }
    }

    public class ZVND_PUTWAY_SAVE_DATA_RFCRequest
    {
        /// <summary>TYPE: WWWOBJID — SAP User ID</summary>
        public string IM_USER { get; set; }

        /// <summary>
        /// TABLE: IT_DATA (ZTT_PUTWAY_SAVE)
        /// Pass as a list of row dictionaries, e.g.:
        /// [ { "FIELD1": "value1", "FIELD2": "value2" }, ... ]
        /// Field names must match the ZTT_PUTWAY_SAVE structure fields exactly.
        /// </summary>
        public List<Dictionary<string, string>> IT_DATA { get; set; }
    }
}
