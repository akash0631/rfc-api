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
        public IHttpActionResult UpdatePurchaseOrderCost(ZPO_COST_UPD_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                IRfcStructure IM_DATA = myfun.GetStructure("IM_DATA");
                if (request.IM_DATA != null)
                {
                    IM_DATA.SetValue("EBELN", request.IM_DATA.EBELN);
                    IM_DATA.SetValue("EBELP", request.IM_DATA.EBELP);
                    IM_DATA.SetValue("NETPR", request.IM_DATA.NETPR);
                    IM_DATA.SetValue("PEINH", request.IM_DATA.PEINH);
                    IM_DATA.SetValue("BPRME", request.IM_DATA.BPRME);
                    IM_DATA.SetValue("BPUMZ", request.IM_DATA.BPUMZ);
                    IM_DATA.SetValue("BPUMN", request.IM_DATA.BPUMN);
                    IM_DATA.SetValue("WAERS", request.IM_DATA.WAERS);
                    IM_DATA.SetValue("UPDAT", request.IM_DATA.UPDAT);
                    IM_DATA.SetValue("UZEIT", request.IM_DATA.UZEIT);
                    IM_DATA.SetValue("UNAME", request.IM_DATA.UNAME);
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

    public class ZPO_COST_UPD_Request
    {
        public ZST_PO_IMP IM_DATA { get; set; }
    }

    public class ZST_PO_IMP
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public decimal NETPR { get; set; }
        public decimal PEINH { get; set; }
        public string BPRME { get; set; }
        public decimal BPUMZ { get; set; }
        public decimal BPUMN { get; set; }
        public string WAERS { get; set; }
        public string UPDAT { get; set; }
        public string UZEIT { get; set; }
        public string UNAME { get; set; }
    }
}