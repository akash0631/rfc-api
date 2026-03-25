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
    public class ZADVANCE_PAYMENT_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZADVANCE_PAYMENT_RFC")]
        public async Task<IHttpActionResult> ProcessAdvancePayment([FromBody] ZADVANCE_PAYMENT_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Invalid request data" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZADVANCE_PAYMENT_RFC");

                myfun.SetValue("IV_BUKRS", request.IV_BUKRS);
                myfun.SetValue("IV_GJAHR", request.IV_GJAHR);
                myfun.SetValue("IV_BELNR", request.IV_BELNR);
                myfun.SetValue("IV_BUZEI", request.IV_BUZEI);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Json(new { Status = "E", Message = EX_RETURN.GetString("MESSAGE") });
                }

                IRfcTable advancePaymentTable = myfun.GetTable("ET_ADVANCE_PAYMENT");
                var advancePaymentData = new List<Dictionary<string, object>>();
                var apMeta = advancePaymentTable.Metadata.LineType;
                for (int i = 0; i < advancePaymentTable.RowCount; i++)
                {
                    advancePaymentTable.CurrentIndex = i;
                    var rowData = new Dictionary<string, object>();
                    for (int j = 0; j < apMeta.FieldCount; j++)
                    {
                        var field = apMeta[j];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                            rowData[field.Name] = advancePaymentTable.GetValue(field.Name)?.ToString() ?? "";
                    }
                    advancePaymentData.Add(rowData);
                }

                var response = new
                {
                    Status = "S",
                    Message = "Advance payment data retrieved successfully",
                    Data = new
                    {
                        ET_ADVANCE_PAYMENT = advancePaymentData
                    }
                };

                return Json(response);
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

    public class ZADVANCE_PAYMENT_RFCRequest
    {
        public string IV_BUKRS { get; set; }
        public string IV_GJAHR { get; set; }
        public string IV_BELNR { get; set; }
        public string IV_BUZEI { get; set; }
    }
}