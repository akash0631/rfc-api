using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.NSO
{
    public class ZTEST_PIPE_CHK_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZTEST_PIPE_CHK_RFC")]
        public async Task<HttpResponseMessage> ZTEST_PIPE_CHK_RFC(ZTEST_PIPE_CHK_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZTEST_PIPE_CHK_RFC");
                
                myfun.SetValue("IV_MATNR", request.IV_MATNR);
                myfun.SetValue("IV_WERKS", request.IV_WERKS);
                
                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    });
                }
                
                IRfcTable etDataTable = myfun.GetTable("ET_DATA");
                List<Dictionary<string, object>> etDataList = new List<Dictionary<string, object>>();
                
                foreach (var row in etDataTable.AsEnumerable())
                {
                    Dictionary<string, object> rowData = new Dictionary<string, object>();
                    IRfcStructure structure = (IRfcStructure)row;
                    
                    for (int i = 0; i < structure.Metadata.FieldCount; i++)
                    {
                        RfcFieldMetadata field = structure.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            rowData[field.Name] = structure.GetValue(field.Name);
                        }
                    }
                    etDataList.Add(rowData);
                }
                
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_DATA = etDataList
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }
    
    public class ZTEST_PIPE_CHK_RFCRequest
    {
        public string IV_MATNR { get; set; }
        public string IV_WERKS { get; set; }
    }
}