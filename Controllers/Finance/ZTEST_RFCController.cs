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
    public class ZTEST_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZTEST_RFC")]
        public object ZTEST_RFC(ZTEST_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZTEST_RFC");
                
                myfun.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                myfun.SetValue("I_DATE", request.I_DATE);
                
                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = (object)null
                    };
                }
                
                IRfcTable tbl = myfun.GetTable("ET_RESULT");
                var resultData = tbl.AsEnumerable().Select(row =>
                {
                    var rowData = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            rowData[field.Name] = row.GetValue(field.Name);
                        }
                    }
                    return rowData;
                }).ToList();
                
                return new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_RESULT = resultData
                    }
                };
            }
            catch (RfcAbapException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                };
            }
            catch (RfcCommunicationException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                };
            }
        }
    }
    
    public class ZTEST_RFCRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_DATE { get; set; }
    }
}