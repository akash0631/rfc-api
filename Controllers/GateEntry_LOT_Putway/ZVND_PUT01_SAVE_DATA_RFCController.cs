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

namespace Vendor_SRM_Routing_Application.Controllers.GateEntry_LOT_Putway
{
    public class ZVND_PUT01_SAVE_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_PUT01_SAVE_DATA_RFC")]
        public async Task<IHttpActionResult> ZVND_PUT01_SAVE_DATA_RFC([FromBody] ZVND_PUT01_SAVE_DATA_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request == null)
                        return Ok(new { Status = "E", Message = "Request cannot be null" });

                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_PUT01_SAVE_DATA_RFC");

                    myfun.SetValue("IM_USER", request.IM_USER ?? "");

                    // Populate IT_DATA table
                    if (request.IT_DATA != null && request.IT_DATA.Count > 0)
                    {
                        IRfcTable itData = myfun.GetTable("IT_DATA");
                        foreach (var item in request.IT_DATA)
                        {
                            itData.Append();
                            foreach (var kv in item)
                                try { itData.SetValue(kv.Key, kv.Value?.ToString() ?? ""); } catch { }
                        }
                    }

                    myfun.Invoke(dest);

                    IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                    string sapType    = exReturn.GetValue("TYPE").ToString();
                    string sapMessage = exReturn.GetValue("MESSAGE").ToString();

                    return Ok(new { Status = sapType == "E" ? "E" : "S", Message = sapMessage });
                }
                catch (Exception ex)
                {
                    return Ok(new { Status = "E", Message = ex.Message });
                }
            });
        }
    }

    public class ZVND_PUT01_SAVE_DATA_RFCRequest
    {
        public string IM_USER { get; set; }
        public List<Dictionary<string, object>> IT_DATA { get; set; }
    }
}
