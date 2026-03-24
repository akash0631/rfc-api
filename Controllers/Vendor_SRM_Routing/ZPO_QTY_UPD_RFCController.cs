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
        public IHttpActionResult ZPO_QTY_UPD_RFC(ZPO_QTY_UPD_RFC_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_QTY_UPD_RFC");

                IRfcTable itDataTable = myfun.GetTable("IT_DATA");
                if (request.IT_DATA != null && request.IT_DATA.Any())
                {
                    foreach (var item in request.IT_DATA)
                    {
                        itDataTable.Append();
                        itDataTable.SetValue("EBELN", item.EBELN);
                        itDataTable.SetValue("EBELP", item.EBELP);
                        itDataTable.SetValue("MENGE", item.MENGE);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string type = EX_RETURN.GetString("TYPE");
                string message = EX_RETURN.GetString("MESSAGE");

                if (type == "E")
                {
                    return Ok(new { Status = "E", Message = message });
                }

                return Ok(new { Status = type, Message = message });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
            catch (CommunicationException ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZPO_QTY_UPD_RFC_Request
    {
        public List<ZST_PO_IMP_QTY> IT_DATA { get; set; }
    }

    public class ZST_PO_IMP_QTY
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public decimal MENGE { get; set; }
    }
}