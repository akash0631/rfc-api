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
    public class ZVND_GATELOT2_PICKLIST_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_GATELOT2_PICKLIST_VAL_RFC")]
        public IHttpActionResult ZVND_GATELOT2_PICKLIST_VAL_RFC([FromBody] ZVND_GATELOT2_PICKLIST_VAL_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_GATELOT2_PICKLIST_VAL_RFC");

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetValue("TYPE").ToString() == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetValue("MESSAGE").ToString()
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_PICKLIST_VAL");
                var picklistData = tbl.AsEnumerable().Select(row =>
                {
                    var rowData = new Dictionary<string, object>();
                    var metadata = row.GetMetadata();
                    
                    for (int i = 0; i < metadata.FieldCount; i++)
                    {
                        var fieldMetadata = metadata[i];
                        if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                        {
                            rowData[fieldMetadata.Name] = row.GetValue(fieldMetadata.Name);
                        }
                    }
                    
                    return rowData;
                }).ToList();

                return Ok(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_PICKLIST_VAL = picklistData
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

    public class ZVND_GATELOT2_PICKLIST_VAL_RFCRequest
    {
    }
}