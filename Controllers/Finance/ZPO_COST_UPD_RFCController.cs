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

                if (request.IM_DATA != null)
                {
                    IRfcStructure imDataStructure = myfun.GetStructure("IM_DATA");
                    if (!string.IsNullOrEmpty(request.IM_DATA.PO_NUMBER))
                        imDataStructure.SetValue("PO_NUMBER", request.IM_DATA.PO_NUMBER);
                    if (!string.IsNullOrEmpty(request.IM_DATA.PO_ITEM))
                        imDataStructure.SetValue("PO_ITEM", request.IM_DATA.PO_ITEM);
                    if (!string.IsNullOrEmpty(request.IM_DATA.COST_AMOUNT))
                        imDataStructure.SetValue("COST_AMOUNT", request.IM_DATA.COST_AMOUNT);
                    if (!string.IsNullOrEmpty(request.IM_DATA.CURRENCY))
                        imDataStructure.SetValue("CURRENCY", request.IM_DATA.CURRENCY);
                    if (!string.IsNullOrEmpty(request.IM_DATA.COST_TYPE))
                        imDataStructure.SetValue("COST_TYPE", request.IM_DATA.COST_TYPE);
                    if (!string.IsNullOrEmpty(request.IM_DATA.VENDOR_CODE))
                        imDataStructure.SetValue("VENDOR_CODE", request.IM_DATA.VENDOR_CODE);
                    if (!string.IsNullOrEmpty(request.IM_DATA.PLANT))
                        imDataStructure.SetValue("PLANT", request.IM_DATA.PLANT);
                    if (!string.IsNullOrEmpty(request.IM_DATA.MATERIAL))
                        imDataStructure.SetValue("MATERIAL", request.IM_DATA.MATERIAL);
                    if (!string.IsNullOrEmpty(request.IM_DATA.UPDATED_BY))
                        imDataStructure.SetValue("UPDATED_BY", request.IM_DATA.UPDATED_BY);
                    if (!string.IsNullOrEmpty(request.IM_DATA.UPDATE_DATE))
                        imDataStructure.SetValue("UPDATE_DATE", request.IM_DATA.UPDATE_DATE);
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = returnMessage
                    });
                }

                return Ok(new
                {
                    Status = "S",
                    Message = returnMessage
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (CommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
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
        public string PO_ITEM { get; set; }
        public string COST_AMOUNT { get; set; }
        public string CURRENCY { get; set; }
        public string COST_TYPE { get; set; }
        public string VENDOR_CODE { get; set; }
        public string PLANT { get; set; }
        public string MATERIAL { get; set; }
        public string UPDATED_BY { get; set; }
        public string UPDATE_DATE { get; set; }
    }
}