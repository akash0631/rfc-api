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
        public async Task<object> ValidateGateLotToPicklist([FromBody] ZVND_GATELOT2_PICKLIST_VAL_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_GATELOT2_PICKLIST_VAL_RFC");

                myfun.SetValue("IV_GATE_LOT", request.IV_GATE_LOT);
                myfun.SetValue("IV_PICKLIST_ID", request.IV_PICKLIST_ID);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    };
                }

                IRfcTable tbl = myfun.GetTable("ET_VALIDATION_RESULT");
                var validationResults = tbl.AsEnumerable().Select(row =>
                {
                    var result = new Dictionary<string, object>();
                    for (int i = 0; i < row.FieldCount; i++)
                    {
                        var field = row[i];
                        result[field.Name] = field.GetValue();
                    }
                    return result;
                }).ToList();

                return new
                {
                    Status = "S",
                    Message = "Gate lot to picklist validation completed successfully",
                    Data = new
                    {
                        ET_VALIDATION_RESULT = validationResults
                    }
                };
            }
            catch (RfcAbapException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message
                };
            }
            catch (CommunicationException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message
                };
            }
        }
    }

    public class ZVND_GATELOT2_PICKLIST_VAL_RFCRequest
    {
        public string IV_GATE_LOT { get; set; }
        public string IV_PICKLIST_ID { get; set; }
    }
}