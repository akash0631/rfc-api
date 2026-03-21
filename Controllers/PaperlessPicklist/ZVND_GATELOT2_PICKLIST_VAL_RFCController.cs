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
        public async Task<object> ValidateGateLot2PicklistData(ZVND_GATELOT2_PICKLIST_VAL_RFC_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_GATELOT2_PICKLIST_VAL_RFC");

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

                IRfcTable etPicklistVal = myfun.GetTable("ET_PICKLIST_VAL");
                
                var picklistData = etPicklistVal.AsEnumerable().Select(row =>
                {
                    var dynamicRow = new Dictionary<string, object>();
                    var metadata = row.GetElementMetadata();
                    
                    foreach (var field in metadata)
                    {
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            try
                            {
                                dynamicRow[field.Name] = row.GetString(field.Name);
                            }
                            catch
                            {
                                dynamicRow[field.Name] = null;
                            }
                        }
                    }
                    return dynamicRow;
                }).ToList();

                return new
                {
                    Status = "S",
                    Message = "Gate Lot 2 picklist validation completed successfully",
                    Data = new
                    {
                        ET_PICKLIST_VAL = picklistData
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

    public class ZVND_GATELOT2_PICKLIST_VAL_RFC_Request
    {
    }
}