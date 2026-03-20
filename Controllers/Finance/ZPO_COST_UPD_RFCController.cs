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

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZPO_COST_UPD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZPO_COST_UPD_RFC/Post")]
        public IHttpActionResult Post([FromBody] ZPO_COST_UPD_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = "Request data is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                // IM_DATA is a TABLE parameter (ZTT_PO_IMP → rows of ZST_PO_IMP)
                if (request.IM_DATA != null && request.IM_DATA.Count > 0)
                {
                    IRfcTable imDataTable = myfun.GetTable("IM_DATA");
                    foreach (var row in request.IM_DATA)
                    {
                        imDataTable.Append();
                        if (!string.IsNullOrEmpty(row.EBELN))    imDataTable.SetValue("EBELN", row.EBELN);
                        if (!string.IsNullOrEmpty(row.MATNR))    imDataTable.SetValue("MATNR", row.MATNR);
                        if (!string.IsNullOrEmpty(row.PO_ITEM))  imDataTable.SetValue("PO_ITEM", row.PO_ITEM);
                        if (!string.IsNullOrEmpty(row.COST))     imDataTable.SetValue("COST", row.COST);
                    }
                }

                myfun.Invoke(dest);

                string msgType    = myfun.GetString("MSG_TYPE");
                string message    = myfun.GetString("MESSAGE");
                bool   status     = msgType == "S";

                return Ok(new
                {
                    Status  = status,
                    MSG_TYPE = msgType,
                    MESSAGE  = message
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message });
            }
        }
    }

    public class ZPO_COST_UPD_RFCRequest
    {
        public List<ZST_PO_IMP> IM_DATA { get; set; }
    }

    public class ZST_PO_IMP
    {
        public string EBELN   { get; set; }  // Purchasing Document Number (10)
        public string MATNR   { get; set; }  // Material Number (40)
        public string PO_ITEM { get; set; }  // Item Number of PO (5)
        public string COST    { get; set; }  // Cost (13)
    }
}
