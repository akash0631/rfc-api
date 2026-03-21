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
        public async Task<IHttpActionResult> ZADVANCE_PAYMENT_RFC([FromBody] ZADVANCE_PAYMENT_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = "Request cannot be null"
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZADVANCE_PAYMENT_RFC");

                myfun.SetValue("IV_VENDOR_ID", request.IV_VENDOR_ID ?? "");
                myfun.SetValue("IV_AMOUNT", request.IV_AMOUNT);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_PAYMENTS");
                var etPayments = tbl.AsEnumerable().Select(row =>
                {
                    var dynamicRow = new Dictionary<string, object>();
                    for (int i = 0; i < row.ElementCount; i++)
                    {
                        var element = row[i];
                        if (element.Metadata.DataType != RfcDataType.STRUCTURE && element.Metadata.DataType != RfcDataType.TABLE)
                        {
                            var fieldName = element.Metadata.Name;
                            var fieldValue = element.GetValue();
                            dynamicRow[fieldName] = fieldValue;
                        }
                    }
                    return dynamicRow;
                }).ToList();

                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_PAYMENTS = etPayments
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }

    public class ZADVANCE_PAYMENT_RFCRequest
    {
        public string IV_VENDOR_ID { get; set; }
        public decimal IV_AMOUNT { get; set; }
    }
}