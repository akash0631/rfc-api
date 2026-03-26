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
    public class ZPO_QTY_UPD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZPO_QTY_UPD_RFC")]
        public IHttpActionResult ZPO_QTY_UPD_RFC(ZPO_QTY_UPD_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_QTY_UPD_RFC");

                IRfcTable IT_DATA = myfun.GetTable("IT_DATA");
                if (request.IT_DATA != null)
                {
                    foreach (var item in request.IT_DATA)
                    {
                        IRfcStructure row = IT_DATA.Metadata.LineType.CreateStructure();
                        for (int i = 0; i < IT_DATA.Metadata.LineType.FieldCount; i++)
                        {
                            string fieldName = IT_DATA.Metadata.LineType[i].Name;
                            var property = item.GetType().GetProperty(fieldName);
                            if (property != null)
                            {
                                row.SetValue(fieldName, property.GetValue(item) ?? "");
                            }
                        }
                        IT_DATA.Append(row);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Ok(new { Status = "E", Message = returnMessage });
                }

                return Ok(new { Status = "S", Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZPO_QTY_UPD_RFCRequest
    {
        public List<dynamic> IT_DATA { get; set; }
    }
}