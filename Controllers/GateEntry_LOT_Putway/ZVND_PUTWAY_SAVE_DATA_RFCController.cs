using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    /// <summary>
    /// ZVND_PUTWAY_SAVE_DATA_RFC — WRITE RFC
    /// Saves putway data after BIN and palette validation. Posts IT_DATA table to SAP.
    /// Module: Gate Entry / LOT Putaway
    /// Route:  POST api/ZVND_PUTWAY_SAVE_DATA_RFC
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

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZVND_PUTWAY_SAVE_DATA_RFC");

                        myfun.SetValue("IM_USER", request.IM_USER);

                        // Populate IT_DATA input table (ZTT_PUTWAY_SAVE)
                        if (request.IT_DATA != null && request.IT_DATA.Count > 0)
                        {
                            IRfcTable itData = myfun.GetTable("IT_DATA");
                            var meta = itData.Metadata.LineType;

                            foreach (var row in request.IT_DATA)
                            {
                                itData.Append();
                                foreach (var kvp in row)
                                {
                                    try
                                    {
                                        for (int i = 0; i < meta.FieldCount; i++)
                                        {
                                            if (string.Equals(meta[i].Name, kvp.Key, StringComparison.OrdinalIgnoreCase))
                                            {
                                                itData.SetValue(kvp.Key, kvp.Value ?? "");
                                                break;
                                            }
                                        }
                                    }
                                    catch { /* skip unknown fields */ }
                                }
                            }
                        }

                        myfun.Invoke(dest);

                        IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");

                        string SAP_TYPE    = E_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();

                        if (SAP_TYPE == "E")
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status  = false,
                                Message = "" + SAP_Message + ""
                            });
                        }
                        else
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status  = true,
                                Message = "" + SAP_Message + ""
                            });
                        }
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status  = false,
                            Message = "Request Not Valid"
                        });
                    }
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
        /// <summary>TYPE: WWWOBJID — SAP User ID</summary>
        public string IM_USER { get; set; }

        /// <summary>
        /// TYPE: ZTT_PUTWAY_SAVE — Input table of putway save records.
        /// Each dictionary entry maps SAP field name → value.
        /// Fields resolved at runtime against the SAP ABAP structure ZTT_PUTWAY_SAVE.
        /// </summary>
        public List<Dictionary<string, string>> IT_DATA { get; set; }
    }
}
