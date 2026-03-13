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
        public async Task<IHttpActionResult> ZTEST_RFC([FromBody] ZTEST_RFCRequest request)
        {
            try
            {
                RfcDestination destination = RfcDestinationManager.GetDestination(BaseController.rfcConfigparameters());
                RfcRepository repository = destination.Repository;
                IRfcFunction function = repository.CreateFunction("ZTEST_RFC");

                function.SetValue("IM_USER", request.IM_USER);
                function.SetValue("IM_WERKS", request.IM_WERKS);

                function.Invoke(destination);

                IRfcTable exReturn = function.GetTable("EX_RETURN");
                if (exReturn.Count > 0)
                {
                    exReturn.CurrentIndex = 0;
                    string returnType = exReturn.GetString("TYPE");
                    if (returnType == "E")
                    {
                        string message = exReturn.GetString("MESSAGE");
                        return Ok(new
                        {
                            Status = "E",
                            Message = message,
                            Data = new { ET_DATA = new List<object>() }
                        });
                    }
                }

                IRfcTable etData = function.GetTable("ET_DATA");
                List<Dictionary<string, object>> dataList = new List<Dictionary<string, object>>();

                for (int i = 0; i < etData.Count; i++)
                {
                    etData.CurrentIndex = i;
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    
                    for (int j = 0; j < etData.Metadata.FieldCount; j++)
                    {
                        RfcFieldMetadata fieldMeta = etData.Metadata[j];
                        if (fieldMeta.DataType != RfcDataType.STRUCTURE && fieldMeta.DataType != RfcDataType.TABLE)
                        {
                            row[fieldMeta.Name] = etData.GetValue(fieldMeta.Name);
                        }
                    }
                    
                    dataList.Add(row);
                }

                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new { ET_DATA = dataList }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_DATA = new List<object>() }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_DATA = new List<object>() }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { ET_DATA = new List<object>() }
                });
            }
        }
    }

    public class ZTEST_RFCRequest
    {
        public string IM_USER { get; set; }
        public string IM_WERKS { get; set; }
    }
}