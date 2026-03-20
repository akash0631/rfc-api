using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    // PUT01 Inbound Process - Save Data RFC
    // Saves validated HU data to SAP after successful PUT01 inbound scanning
    public class ZVND_PUT01_SAVE_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_PUT01_SAVE_DATA_RFC")]
        public IHttpActionResult Post([FromBody] ZVND_PUT01_SAVE_Request request)
        {
            try
            {
                if (request == null || request.IT_DATA == null || request.IT_DATA.Count == 0)
                    return Ok(new { Status = "E", Message = "IT_DATA table is required." });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("ZVND_PUT01_SAVE_DATA_RFC");

                myfun.SetValue("IM_USER", request.IM_USER ?? string.Empty);

                IRfcTable itData = myfun.GetTable("IT_DATA");
                foreach (var row in request.IT_DATA)
                {
                    itData.Append();
                    foreach (var kv in row)
                        itData.SetValue(kv.Key, kv.Value ?? string.Empty);
                }

                myfun.Invoke(dest);

                IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                string type    = exReturn.GetString("TYPE");
                string message = exReturn.GetString("MESSAGE");

                if (type == "E")
                    return Ok(new { Status = "E", Message = message });

                return Ok(new { Status = "S", Message = message ?? "Data saved successfully." });
            }
            catch (RfcAbapException ex)          { return Ok(new { Status = "E", Message = ex.Message }); }
            catch (RfcCommunicationException ex) { return Ok(new { Status = "E", Message = ex.Message }); }
            catch (Exception ex)                 { return Ok(new { Status = "E", Message = ex.Message }); }
        }
    }

    public class ZVND_PUT01_SAVE_Request
    {
        public string IM_USER { get; set; }
        public List<Dictionary<string, string>> IT_DATA { get; set; }
    }
}
