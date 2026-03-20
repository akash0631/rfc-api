using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    // HU Picking From BIN (GATELOT2) - BIN Validation RFC
    // Validates BIN location for the given Picklist in SAP
    public class ZVND_GATELOT2_BIN_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_GATELOT2_BIN_VAL_RFC")]
        public IHttpActionResult Post([FromBody] ZVND_GATELOT2_BIN_VAL_Request request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = "E", Message = "Request body is required." });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("ZVND_GATELOT2_BIN_VAL_RFC");

                myfun.SetValue("IM_USER",     request.IM_USER     ?? string.Empty);
                myfun.SetValue("IM_PLANT",    request.IM_PLANT    ?? string.Empty);
                myfun.SetValue("IM_PICKLIST", request.IM_PICKLIST ?? string.Empty);
                myfun.SetValue("IM_BIN",      request.IM_BIN      ?? string.Empty);

                myfun.Invoke(dest);

                IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                string type    = exReturn.GetString("TYPE");
                string message = exReturn.GetString("MESSAGE");

                if (type == "E")
                    return Ok(new { Status = "E", Message = message });

                return Ok(new { Status = "S", Message = message ?? "BIN validated successfully." });
            }
            catch (RfcAbapException ex)          { return Ok(new { Status = "E", Message = ex.Message }); }
            catch (RfcCommunicationException ex) { return Ok(new { Status = "E", Message = ex.Message }); }
            catch (Exception ex)                 { return Ok(new { Status = "E", Message = ex.Message }); }
        }
    }

    public class ZVND_GATELOT2_BIN_VAL_Request
    {
        public string IM_USER     { get; set; }
        public string IM_PLANT    { get; set; }
        public string IM_PICKLIST { get; set; }
        public string IM_BIN      { get; set; }
    }
}
