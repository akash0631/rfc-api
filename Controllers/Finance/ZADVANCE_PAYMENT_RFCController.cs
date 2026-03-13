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
        public async Task<object> ZADVANCE_PAYMENT_RFC(ZADVANCE_PAYMENT_RFC_Request request)
        {
            try
            {
                RfcDestination rfcDestination = RfcDestinationManager.GetDestination(rfcConfigparameters);
                RfcRepository rfcRepository = rfcDestination.Repository;
                IRfcFunction rfcFunction = rfcRepository.CreateFunction("ZADVANCE_PAYMENT_RFC");

                rfcFunction.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                rfcFunction.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                rfcFunction.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                rfcFunction.Invoke(rfcDestination);

                IRfcStructure exReturn = rfcFunction.GetStructure("EX_RETURN");
                if (exReturn != null && exReturn.GetValue("TYPE").ToString() == "E")
                {
                    return new
                    {
                        Status = "E",
                        Message = exReturn.GetValue("MESSAGE").ToString(),
                        Data = new { ET_ADVANCE_PAYMENT = new object[0] }
                    };
                }

                IRfcTable etAdvancePaymentTable = rfcFunction.GetTable("ET_ADVANCE_PAYMENT");
                List<Dictionary<string, object>> advancePaymentData = new List<Dictionary<string, object>>();

                for (int i = 0; i < etAdvancePaymentTable.RowCount; i++)
                {
                    etAdvancePaymentTable.CurrentIndex = i;
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    
                    for (int j = 0; j < etAdvancePaymentTable.Metadata.FieldCount; j++)
                    {
                        var field = etAdvancePaymentTable.Metadata[j];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            row[field.Name] = etAdvancePaymentTable.GetValue(field.Name);
                        }
                    }
                    advancePaymentData.Add(row);
                }

                return new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_ADVANCE_PAYMENT = advancePaymentData
                    }
                };
            }
            catch (RfcAbapException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_ADVANCE_PAYMENT = new object[0] }
                };
            }
            catch (RfcCommunicationException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_ADVANCE_PAYMENT = new object[0] }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_ADVANCE_PAYMENT = new object[0] }
                };
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