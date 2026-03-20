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
        public IHttpActionResult UpdatePOCost([FromBody] ZPO_COST_UPD_RFC_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Request data is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                IRfcStructure imDataStruct = myfun.GetStructure("IM_DATA");
                if (request.IM_DATA != null)
                {
                    if (!string.IsNullOrEmpty(request.IM_DATA.PO_NUMBER))
                        imDataStruct.SetValue("PO_NUMBER", request.IM_DATA.PO_NUMBER);
                    if (!string.IsNullOrEmpty(request.IM_DATA.ITEM_NUMBER))
                        imDataStruct.SetValue("ITEM_NUMBER", request.IM_DATA.ITEM_NUMBER);
                    if (request.IM_DATA.COST_AMOUNT.HasValue)
                        imDataStruct.SetValue("COST_AMOUNT", request.IM_DATA.COST_AMOUNT.Value);
                    if (!string.IsNullOrEmpty(request.IM_DATA.CURRENCY))
                        imDataStruct.SetValue("CURRENCY", request.IM_DATA.CURRENCY);
                    if (!string.IsNullOrEmpty(request.IM_DATA.COST_TYPE))
                        imDataStruct.SetValue("COST_TYPE", request.IM_DATA.COST_TYPE);
                    if (!string.IsNullOrEmpty(request.IM_DATA.VENDOR_CODE))
                        imDataStruct.SetValue("VENDOR_CODE", request.IM_DATA.VENDOR_CODE);
                    if (request.IM_DATA.EFFECTIVE_DATE.HasValue)
                        imDataStruct.SetValue("EFFECTIVE_DATE", request.IM_DATA.EFFECTIVE_DATE.Value);
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Json(new { Status = "E", Message = returnMessage });
                }

                return Json(new { Status = "S", Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
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
        public decimal? COST_AMOUNT { get; set; }
        public string CURRENCY { get; set; }
        public string COST_TYPE { get; set; }
        public string VENDOR_CODE { get; set; }
        public DateTime? EFFECTIVE_DATE { get; set; }
    }
}