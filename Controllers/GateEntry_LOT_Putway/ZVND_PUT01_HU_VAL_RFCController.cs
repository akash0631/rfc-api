// PUT01 HU Validation RFC — deploy 1774083213
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
    public class ZVND_PUT01_HU_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_PUT01_HU_VAL_RFC")]
        public async Task<IHttpActionResult> ZVND_PUT01_HU_VAL_RFC([FromBody] ZVND_PUT01_HU_VAL_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request == null)
                        return Ok(new { Status = "E", Message = "Request cannot be null", Data = new { ET_DATA = new List<object>() } });

                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_PUT01_HU_VAL_RFC");

                    myfun.SetValue("IM_USER",  request.IM_USER  ?? "");
                    myfun.SetValue("IM_PLANT", request.IM_PLANT ?? "");
                    myfun.SetValue("IM_HU",    request.IM_HU    ?? "");

                    myfun.Invoke(dest);

                    IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                    string sapType    = exReturn.GetValue("TYPE").ToString();
                    string sapMessage = exReturn.GetValue("MESSAGE").ToString();

                    if (sapType == "E")
                        return Ok(new { Status = "E", Message = sapMessage, Data = new { ET_DATA = new List<object>() } });

                    IRfcTable tbl = myfun.GetTable("ET_DATA");
                    var rows = new List<Dictionary<string, object>>();
                    for (int i = 0; i < tbl.RowCount; i++)
                    {
                        tbl.CurrentIndex = i;
                        var row = new Dictionary<string, object>();
                        var meta = tbl.Metadata.LineType;
                        for (int j = 0; j < meta.FieldCount; j++)
                        {
                            var field = meta[j];
                            if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                                row[field.Name] = tbl.GetValue(field.Name)?.ToString() ?? "";
                        }
                        rows.Add(row);
                    }

                    return Ok(new { Status = "S", Message = sapMessage, Data = new { ET_DATA = rows } });
                }
                catch (Exception ex)
                {
                    return Ok(new { Status = "E", Message = ex.Message, Data = new { ET_DATA = new List<object>() } });
                }
            });
        }
    }

    public class ZVND_PUT01_HU_VAL_RFCRequest
    {
        public string IM_USER  { get; set; }
        public string IM_PLANT { get; set; }
        public string IM_HU    { get; set; }
    }
}
