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
    public class ZPO_MODIFICATIONController : BaseController
    {
        [HttpPost]
        [Route("api/ZPO_MODIFICATION/Execute")]
        public IHttpActionResult Execute([FromBody] ZPO_MODIFICATIONRequest request)
        {
            try
            {
                if (request == null)
                    return Json(new { Status = "E", Message = "Request cannot be null" });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_MODIFICATION");

                if (!string.IsNullOrEmpty(request.IM_PO_NO))
                    myfun.SetValue("IM_PO_NO", request.IM_PO_NO);
                if (!string.IsNullOrEmpty(request.IM_PO_DEL_DATE))
                    myfun.SetValue("IM_PO_DEL_DATE", request.IM_PO_DEL_DATE);
                if (!string.IsNullOrEmpty(request.IM_DEL_CHG_DATE_LOW))
                    myfun.SetValue("IM_DEL_CHG_DATE_LOW", request.IM_DEL_CHG_DATE_LOW);
                if (!string.IsNullOrEmpty(request.IM_DEL_CHG_DATE_HIGH))
                    myfun.SetValue("IM_DEL_CHG_DATE_HIGH", request.IM_DEL_CHG_DATE_HIGH);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                if (EX_RETURN.GetString("TYPE") == "E")
                    return Json(new { Status = "E", Message = EX_RETURN.GetString("MESSAGE") });

                IRfcTable outputTable = myfun.GetTable("ET_PO_OUTPUT");
                var outputData = outputTable.AsEnumerable().Select(row =>
                {
                    var rowData = new Dictionary<string, object>();
                    int fieldCount = outputTable.Metadata.LineType.FieldCount;
                    for (int i = 0; i < fieldCount; i++)
                    {
                        var fieldMeta = outputTable.Metadata.LineType[i];
                        if (fieldMeta.DataType != RfcDataType.STRUCTURE && fieldMeta.DataType != RfcDataType.TABLE)
                            rowData[fieldMeta.Name] = row.GetValue(fieldMeta.Name);
                    }
                    return rowData;
                }).ToList();

                return Json(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new { ET_PO_OUTPUT = outputData }
                });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZPO_MODIFICATIONRequest
    {
        public string IM_PO_NO { get; set; }
        public string IM_PO_DEL_DATE { get; set; }
        public string IM_DEL_CHG_DATE_LOW { get; set; }
        public string IM_DEL_CHG_DATE_HIGH { get; set; }
    }
}
