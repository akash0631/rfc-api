using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using System.Linq;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    [RoutePrefix("api")]
    public class ZFI_PI_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("ZFI_PI_DATA_RFC")]
        public IHttpActionResult ProcessFinancialPostingInterface(ZFI_PI_DATA_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");

                if (request.IT_POSTING_LOW != null && request.IT_POSTING_LOW.Count > 0)
                {
                    IRfcTable postingLowTable = myfun.GetTable("IT_POSTING_LOW");
                    foreach (var item in request.IT_POSTING_LOW)
                    {
                        var row = postingLowTable.Metadata.LineType.CreateStructure();
                        foreach (var prop in item.GetType().GetProperties())
                        {
                            if (row.Metadata.Contains(prop.Name.ToUpper()))
                            {
                                var value = prop.GetValue(item);
                                if (value != null)
                                {
                                    row.SetValue(prop.Name.ToUpper(), value.ToString());
                                }
                            }
                        }
                        postingLowTable.Append(row);
                    }
                }

                if (request.IT_POSTING_HIGH != null && request.IT_POSTING_HIGH.Count > 0)
                {
                    IRfcTable postingHighTable = myfun.GetTable("IT_POSTING_HIGH");
                    foreach (var item in request.IT_POSTING_HIGH)
                    {
                        var row = postingHighTable.Metadata.LineType.CreateStructure();
                        foreach (var prop in item.GetType().GetProperties())
                        {
                            if (row.Metadata.Contains(prop.Name.ToUpper()))
                            {
                                var value = prop.GetValue(item);
                                if (value != null)
                                {
                                    row.SetValue(prop.Name.ToUpper(), value.ToString());
                                }
                            }
                        }
                        postingHighTable.Append(row);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = returnMessage
                    });
                }

                IRfcTable resultTable = myfun.GetTable("IT_FINAL");
                var finalData = new List<Dictionary<string, object>>();

                foreach (IRfcStructure row in resultTable.AsEnumerable())
                {
                    var rowData = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            var value = row.GetValue(field.Name);
                            rowData[field.Name] = value;
                        }
                    }
                    finalData.Add(rowData);
                }

                return Ok(new
                {
                    Status = "S",
                    Message = returnMessage,
                    Data = new
                    {
                        IT_FINAL = finalData
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

    public class ZFI_PI_DATA_RFCRequest
    {
        public List<dynamic> IT_POSTING_LOW { get; set; }
        public List<dynamic> IT_POSTING_HIGH { get; set; }
    }
}