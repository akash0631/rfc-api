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
        public IHttpActionResult UpdatePurchaseOrderCost(ZPO_COST_UPD_RFC_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                IRfcStructure imDataStructure = myfun.GetStructure("IM_DATA");
                imDataStructure.SetValue("PO_NUMBER", request.IM_DATA.PO_NUMBER);
                imDataStructure.SetValue("ITEM_NUMBER", request.IM_DATA.ITEM_NUMBER);
                imDataStructure.SetValue("COST_VALUE", request.IM_DATA.COST_VALUE);
                imDataStructure.SetValue("CURRENCY", request.IM_DATA.CURRENCY);
                imDataStructure.SetValue("EFFECTIVE_DATE", request.IM_DATA.EFFECTIVE_DATE);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Ok(new { Status = "E", Message = returnMessage });
                }

                return Ok(new { Status = returnType, Message = returnMessage });
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

    public class ZPO_COST_UPD_RFC_Request
    {
        public ZST_PO_IMP IM_DATA { get; set; }
    }

    public class ZST_PO_IMP
    {
        public string PO_NUMBER { get; set; }
        public string ITEM_NUMBER { get; set; }
        public decimal COST_VALUE { get; set; }
        public string CURRENCY { get; set; }
        public string EFFECTIVE_DATE { get; set; }
    }
}