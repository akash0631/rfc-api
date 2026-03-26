// Deploy timestamp: 2026-03-26T09:47:48.990Z
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
    public class ZFI_PI_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_PI_DATA_RFC")]
        public IHttpActionResult ProcessFinancePostingInvoiceData([FromBody] ZFI_PI_DATA_RFC_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");

                // Set IT_POSTING_LOW table
                IRfcTable itPostingLow = myfun.GetTable("IT_POSTING_LOW");
                if (request.IT_POSTING_LOW != null)
                {
                    foreach (var item in request.IT_POSTING_LOW)
                    {
                        itPostingLow.Append();
                        foreach (var kvp in item)
                        {
                            try { itPostingLow.SetValue(kvp.Key, kvp.Value != null ? kvp.Value.ToString() : ""); }
                            catch { /* skip unknown fields */ }
                        }
                    }
                }

                // Set IT_POSTING_HIGH table
                IRfcTable itPostingHigh = myfun.GetTable("IT_POSTING_HIGH");
                if (request.IT_POSTING_HIGH != null)
                {
                    foreach (var item in request.IT_POSTING_HIGH)
                    {
                        itPostingHigh.Append();
                        foreach (var kvp in item)
                        {
                            try { itPostingHigh.SetValue(kvp.Key, kvp.Value != null ? kvp.Value.ToString() : ""); }
                            catch { /* skip unknown fields */ }
                        }
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new { Status = "E", Message = EX_RETURN.GetString("MESSAGE") });
                }

                IRfcTable tbl = myfun.GetTable("IT_FINAL");
                var itFinalData = tbl.AsEnumerable().Select(row =>
                {
                    var record = new Dictionary<string, object>();
                    for (int i = 0; i < row.ElementCount; i++)
                    {
                        RfcFieldMetadata field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            record[field.Name] = row.GetString(field.Name);
                        }
                    }
                    return record;
                }).ToList();

                return Ok(new { Status = "S", Message = "Success", Data = new { IT_FINAL = itFinalData } });
            }
            catch (RfcAbapException ex) { return Ok(new { Status = "E", Message = ex.Message }); }
            catch (RfcCommunicationException ex) { return Ok(new { Status = "E", Message = ex.Message }); }
            catch (Exception ex) { return Ok(new { Status = "E", Message = ex.Message }); }
        }
    }

    public class ZFI_PI_DATA_RFC_Request
    {
        public List<Dictionary<string, object>> IT_POSTING_LOW { get; set; }
        public List<Dictionary<string, object>> IT_POSTING_HIGH { get; set; }
    }
}
