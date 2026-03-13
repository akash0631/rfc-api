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
    public class ZGET_STORE_MASTER_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZGET_STORE_MASTER_RFC")]
        public async Task<IHttpActionResult> GetStoreMasterData(ZGET_STORE_MASTER_RFCRequest request)
        {
            try
            {
                RfcDestination destination = RfcDestinationManager.GetDestination(BaseController.rfcConfigparameters);
                RfcRepository repository = destination.Repository;
                IRfcFunction function = repository.CreateFunction("ZGET_STORE_MASTER_RFC");

                if (function == null)
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = "RFC function ZGET_STORE_MASTER_RFC not found",
                        Data = (object)null
                    });
                }

                function.SetValue("IV_STORE_CODE", request.IV_STORE_CODE);

                function.Invoke(destination);

                IRfcTable returnTable = function.GetTable("EX_RETURN");
                if (returnTable.RowCount > 0)
                {
                    returnTable.CurrentIndex = 0;
                    string messageType = returnTable.GetValue("TYPE").ToString();
                    if (messageType == "E")
                    {
                        string errorMessage = returnTable.GetValue("MESSAGE").ToString();
                        return Ok(new
                        {
                            Status = "E",
                            Message = errorMessage,
                            Data = (object)null
                        });
                    }
                }

                IRfcTable storeTable = function.GetTable("ET_STORE_LIST");
                List<Dictionary<string, object>> storeList = new List<Dictionary<string, object>>();

                for (int i = 0; i < storeTable.RowCount; i++)
                {
                    storeTable.CurrentIndex = i;
                    Dictionary<string, object> row = new Dictionary<string, object>();

                    IRfcStructure currentRow = storeTable.CurrentRow;
                    for (int j = 0; j < currentRow.Metadata.FieldCount; j++)
                    {
                        RfcFieldMetadata fieldMetadata = currentRow.Metadata[j];
                        
                        if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                        {
                            string fieldName = fieldMetadata.Name;
                            object fieldValue = currentRow.GetValue(fieldName);
                            row[fieldName] = fieldValue;
                        }
                    }
                    
                    storeList.Add(row);
                }

                return Ok(new
                {
                    Status = "S",
                    Message = "Store master data retrieved successfully",
                    Data = new
                    {
                        ET_STORE_LIST = storeList
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
        }
    }

    public class ZGET_STORE_MASTER_RFCRequest
    {
        public string IV_STORE_CODE { get; set; }
    }
}