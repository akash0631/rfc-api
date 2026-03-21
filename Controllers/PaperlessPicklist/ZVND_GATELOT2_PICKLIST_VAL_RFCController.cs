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
        public async Task<IHttpActionResult> ZVND_GATELOT2_PICKLIST_VAL_RFC(ZVND_GATELOT2_PICKLIST_VAL_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_GATELOT2_PICKLIST_VAL_RFC");

                myfun.SetValue("IV_PLANT", request.IV_PLANT);
                myfun.SetValue("IV_LOT", request.IV_LOT);
                myfun.SetValue("IV_MATERIAL", request.IV_MATERIAL);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_VALIDATION");
                var etValidationData = tbl.AsEnumerable().Select(row =>
                {
                    var rowData = new Dictionary<string, object>();
                    for (int i = 0; i < row.ElementCount; i++)
                    {
                        var metadata = row.GetElementMetadata(i);
                        if (metadata.DataType != RfcDataType.STRUCTURE && metadata.DataType != RfcDataType.TABLE)
                        {
                            rowData[metadata.Name] = row.GetValue(i);
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
                        ET_VALIDATION = etValidationData
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

    public class ZVND_GATELOT2_PICKLIST_VAL_RFCRequest
    {
        public string IV_PLANT { get; set; }
        public string IV_LOT { get; set; }
        public string IV_MATERIAL { get; set; }
    }
}