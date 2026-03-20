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
    public class ZPO_DD_UPD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZPO_DD_UPD_RFC/Post")]
        public IHttpActionResult Post([FromBody] ZPO_DD_UPD_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_DD_UPD_RFC");

                if (!string.IsNullOrEmpty(request.PO_NO))
                    myfun.SetValue("PO_NO", request.PO_NO);
                if (!string.IsNullOrEmpty(request.DELV_DATE))
                    myfun.SetValue("DELV_DATE", request.DELV_DATE);

                myfun.Invoke(dest);

                string msgType = myfun.GetString("MSG_TYPE");
                string message = myfun.GetString("MESSAGE");

                bool success = msgType == "S" || msgType == "";
                return Json(new
                {
                    Status  = success,
                    MSG_TYPE = msgType,
                    MESSAGE  = message
                });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
            catch (CommunicationException ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = false, Message = ex.Message });
            }
        }
    }

    public class ZPO_DD_UPD_RFCRequest
    {
        /// <summary>Purchase Order Number (EBELN)</summary>
        public string PO_NO { get; set; }

        /// <summary>New Delivery Date — YYYYMMDD or DD.MM.YYYY</summary>
        public string DELV_DATE { get; set; }
    }
}
