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
        public async Task<IHttpActionResult> UpdatePurchaseOrderCost([FromBody] ZPO_COST_UPD_Request request)
        {
            try
            {
                if (request == null || request.IM_DATA == null)
                {
                    return Ok(new { Status = "E", Message = "Invalid request data" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                IRfcStructure imData = myfun.GetStructure("IM_DATA");
                if (!string.IsNullOrEmpty(request.IM_DATA.PO_NUMBER))
                    imData.SetValue("PO_NUMBER", request.IM_DATA.PO_NUMBER);
                if (!string.IsNullOrEmpty(request.IM_DATA.COMPANY_CODE))
                    imData.SetValue("COMPANY_CODE", request.IM_DATA.COMPANY_CODE);
                if (!string.IsNullOrEmpty(request.IM_DATA.PLANT))
                    imData.SetValue("PLANT", request.IM_DATA.PLANT);
                if (!string.IsNullOrEmpty(request.IM_DATA.VENDOR_CODE))
                    imData.SetValue("VENDOR_CODE", request.IM_DATA.VENDOR_CODE);
                if (!string.IsNullOrEmpty(request.IM_DATA.MATERIAL_CODE))
                    imData.SetValue("MATERIAL_CODE", request.IM_DATA.MATERIAL_CODE);
                if (request.IM_DATA.QUANTITY.HasValue)
                    imData.SetValue("QUANTITY", request.IM_DATA.QUANTITY.Value);
                if (request.IM_DATA.UNIT_PRICE.HasValue)
                    imData.SetValue("UNIT_PRICE", request.IM_DATA.UNIT_PRICE.Value);
                if (request.IM_DATA.TOTAL_COST.HasValue)
                    imData.SetValue("TOTAL_COST", request.IM_DATA.TOTAL_COST.Value);
                if (!string.IsNullOrEmpty(request.IM_DATA.CURRENCY))
                    imData.SetValue("CURRENCY", request.IM_DATA.CURRENCY);
                if (!string.IsNullOrEmpty(request.IM_DATA.UPDATED_BY))
                    imData.SetValue("UPDATED_BY", request.IM_DATA.UPDATED_BY);
                if (!string.IsNullOrEmpty(request.IM_DATA.UPDATE_DATE))
                    imData.SetValue("UPDATE_DATE", request.IM_DATA.UPDATE_DATE);

                myfun.SetValue("IM_DATA", imData);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE")?.ToString() ?? "";
                string returnMessage = EX_RETURN.GetValue("MESSAGE")?.ToString() ?? "";

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
        public string PO_NUMBER { get; set; }
        public string COMPANY_CODE { get; set; }
        public string PLANT { get; set; }
        public string VENDOR_CODE { get; set; }
        public string MATERIAL_CODE { get; set; }
        public decimal? QUANTITY { get; set; }
        public decimal? UNIT_PRICE { get; set; }
        public decimal? TOTAL_COST { get; set; }
        public string CURRENCY { get; set; }
        public string UPDATED_BY { get; set; }
        public string UPDATE_DATE { get; set; }
    }
}