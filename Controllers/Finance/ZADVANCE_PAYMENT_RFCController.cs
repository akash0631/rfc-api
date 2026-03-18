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
        public IHttpActionResult ZADVANCE_PAYMENT_RFC(ZADVANCE_PAYMENT_RFC_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZADVANCE_PAYMENT_RFC");

                myfun.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                myfun.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                myfun.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = new
                        {
                            ET_ADVANCE_PAYMENT = new List<dynamic>()
                        }
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_ADVANCE_PAYMENT");
                var advancePaymentData = tbl.AsEnumerable().Select(row =>
                {
                    var dynamicRow = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata.GetField(i);
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            dynamicRow[field.Name] = row.GetValue(field.Name);
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
                        ET_ADVANCE_PAYMENT = advancePaymentData
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
            catch (CommunicationException ex)
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

    public class ZADVANCE_PAYMENT_RFC_Request
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}