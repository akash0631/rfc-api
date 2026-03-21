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
        public IHttpActionResult ProcessAdvancePayment([FromBody] ZADVANCE_PAYMENT_RFCRequest request)
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

                myfun.SetValue("I_BUKRS", request.I_BUKRS);
                myfun.SetValue("I_GJAHR", request.I_GJAHR);
                myfun.SetValue("I_BELNR", request.I_BELNR);

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

                IRfcTable paymentTable = myfun.GetTable("ET_PAYMENT_DATA");
                var paymentData = paymentTable.AsEnumerable().Select(row =>
                {
                    var result = new Dictionary<string, object>();
                    var metadata = row.GetMetadata();
                    
                    for (int i = 0; i < metadata.FieldCount; i++)
                    {
                        var fieldMetadata = metadata[i];
                        if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                        {
                            result[fieldMetadata.Name] = row.GetValue(fieldMetadata.Name);
                        }
                    }
                    
                    return result;
                }).ToList();

                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_PAYMENT_DATA = paymentData
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
        public string I_BUKRS { get; set; }
        public string I_GJAHR { get; set; }
        public string I_BELNR { get; set; }
    }
}