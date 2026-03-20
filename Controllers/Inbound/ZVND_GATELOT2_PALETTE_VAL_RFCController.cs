using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    // HU Picking From BIN (GATELOT2) - Palette Validation RFC
    // Validates Palette and returns ET_BIN, ET_PALL and ET_BOX (HU) data from SAP
    public class ZVND_GATELOT2_PALETTE_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_GATELOT2_PALETTE_VAL_RFC")]
        public IHttpActionResult Post([FromBody] ZVND_GATELOT2_PALETTE_VAL_Request request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = "E", Message = "Request body is required.",
                        Data = new { ET_BIN = string.Empty, ET_PALL = string.Empty, ET_BOX = new List<object>() } });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("ZVND_GATELOT2_PALETTE_VAL_RFC");

                myfun.SetValue("IM_USER",     request.IM_USER     ?? string.Empty);
                myfun.SetValue("IM_PLANT",    request.IM_PLANT    ?? string.Empty);
                myfun.SetValue("IM_PICKLIST", request.IM_PICKLIST ?? string.Empty);
                myfun.SetValue("IM_BIN",      request.IM_BIN      ?? string.Empty);
                myfun.SetValue("IM_PALL",     request.IM_PALL     ?? string.Empty);

                myfun.Invoke(dest);

                IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                string type    = exReturn.GetString("TYPE");
                string message = exReturn.GetString("MESSAGE");

                if (type == "E")
                    return Ok(new { Status = "E", Message = message,
                        Data = new { ET_BIN = string.Empty, ET_PALL = string.Empty, ET_BOX = new List<object>() } });

                string etBin  = myfun.GetString("ET_BIN");
                string etPall = myfun.GetString("ET_PALL");

                IRfcTable tblBox = myfun.GetTable("ET_BOX");
                var boxes = new List<Dictionary<string, object>>();
                foreach (IRfcStructure row in tblBox)
                {
                    var rec = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var f = row.Metadata[i];
                        if (f.DataType != RfcDataType.STRUCTURE && f.DataType != RfcDataType.TABLE)
                            rec[f.Name] = row.GetString(f.Name);
                    }
                    boxes.Add(rec);
                }

                return Ok(new { Status = "S", Message = message ?? "Palette validated successfully.",
                    Data = new { ET_BIN = etBin, ET_PALL = etPall, ET_BOX = boxes } });
            }
            catch (RfcAbapException ex)          { return Ok(new { Status = "E", Message = ex.Message, Data = new { ET_BIN = string.Empty, ET_PALL = string.Empty, ET_BOX = new List<object>() } }); }
            catch (RfcCommunicationException ex) { return Ok(new { Status = "E", Message = ex.Message, Data = new { ET_BIN = string.Empty, ET_PALL = string.Empty, ET_BOX = new List<object>() } }); }
            catch (Exception ex)                 { return Ok(new { Status = "E", Message = ex.Message, Data = new { ET_BIN = string.Empty, ET_PALL = string.Empty, ET_BOX = new List<object>() } }); }
        }
    }

    public class ZVND_GATELOT2_PALETTE_VAL_Request
    {
        public string IM_USER     { get; set; }
        public string IM_PLANT    { get; set; }
        public string IM_PICKLIST { get; set; }
        public string IM_BIN      { get; set; }
        public string IM_PALL     { get; set; }
    }
}
