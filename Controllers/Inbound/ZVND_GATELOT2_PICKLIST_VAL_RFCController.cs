using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    // HU Picking From BIN (GATELOT2) - Picklist Validation RFC
    // Validates Picklist No and auto-fetches PO No and INV No from SAP
    public class ZVND_GATELOT2_PICKLIST_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_GATELOT2_PICKLIST_VAL_RFC")]
        public IHttpActionResult Post([FromBody] ZVND_GATELOT2_PICKLIST_VAL_Request request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = "E", Message = "Request body is required.", Data = new { ET_DATA = new List<object>() } });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("ZVND_GATELOT2_PICKLIST_VAL_RFC");

                myfun.SetValue("IM_USER",  request.IM_USER  ?? string.Empty);
                myfun.SetValue("IM_PLANT", request.IM_PLANT ?? string.Empty);

                myfun.Invoke(dest);

                IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                string type    = exReturn.GetString("TYPE");
                string message = exReturn.GetString("MESSAGE");

                if (type == "E")
                    return Ok(new { Status = "E", Message = message, Data = new { ET_DATA = new List<object>() } });

                IRfcTable tbl = myfun.GetTable("ET_DATA");
                var rows = new List<Dictionary<string, object>>();
                foreach (IRfcStructure row in tbl)
                {
                    var rec = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var f = row.Metadata[i];
                        if (f.DataType != RfcDataType.STRUCTURE && f.DataType != RfcDataType.TABLE)
                            rec[f.Name] = row.GetString(f.Name);
                    }
                    rows.Add(rec);
                }

                return Ok(new { Status = "S", Message = message ?? "Success", Data = new { ET_DATA = rows } });
            }
            catch (RfcAbapException ex)          { return Ok(new { Status = "E", Message = ex.Message, Data = new { ET_DATA = new List<object>() } }); }
            catch (RfcCommunicationException ex) { return Ok(new { Status = "E", Message = ex.Message, Data = new { ET_DATA = new List<object>() } }); }
            catch (Exception ex)                 { return Ok(new { Status = "E", Message = ex.Message, Data = new { ET_DATA = new List<object>() } }); }
        }
    }

    public class ZVND_GATELOT2_PICKLIST_VAL_Request
    {
        public string IM_USER  { get; set; }
        public string IM_PLANT { get; set; }
    }
}
