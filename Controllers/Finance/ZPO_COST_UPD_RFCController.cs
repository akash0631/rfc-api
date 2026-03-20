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
    public class ZPO_COST_UPD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZPO_COST_UPD_RFC")]
        public IHttpActionResult UpdatePurchaseOrderCost([FromBody] ZPO_COST_UPD_RFCRequest request)
        {
            try
            {
                if (request == null || request.IM_DATA == null)
                {
                    return Json(new { Status = "E", Message = "Invalid request data" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                IRfcStructure imDataStructure = myfun.GetStructure("IM_DATA");
                if (!string.IsNullOrEmpty(request.IM_DATA.EBELN))
                    imDataStructure.SetValue("EBELN", request.IM_DATA.EBELN);
                if (!string.IsNullOrEmpty(request.IM_DATA.EBELP))
                    imDataStructure.SetValue("EBELP", request.IM_DATA.EBELP);
                if (!string.IsNullOrEmpty(request.IM_DATA.NETPR))
                    imDataStructure.SetValue("NETPR", request.IM_DATA.NETPR);
                if (!string.IsNullOrEmpty(request.IM_DATA.WAERS))
                    imDataStructure.SetValue("WAERS", request.IM_DATA.WAERS);
                if (!string.IsNullOrEmpty(request.IM_DATA.MWSKZ))
                    imDataStructure.SetValue("MWSKZ", request.IM_DATA.MWSKZ);

                myfun.SetValue("IM_DATA", imDataStructure);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string type = EX_RETURN.GetValue("TYPE")?.ToString() ?? "";
                string message = EX_RETURN.GetValue("MESSAGE")?.ToString() ?? "";

                if (type == "E")
                {
                    return Json(new { Status = "E", Message = message });
                }

                return Json(new { Status = "S", Message = message });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (CommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZPO_COST_UPD_RFCRequest
    {
        public ZST_PO_IMP IM_DATA { get; set; }
    }

    public class ZST_PO_IMP
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string NETPR { get; set; }
        public string WAERS { get; set; }
        public string MWSKZ { get; set; }
    }
}