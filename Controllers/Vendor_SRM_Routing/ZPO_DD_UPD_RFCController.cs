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
        [Route("api/ZPO_DD_UPD_RFC")]
        public IHttpActionResult UpdatePurchaseOrderDeliveryDate([FromBody] ZPO_DD_UPD_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new { Status = "E", Message = "Request cannot be null" });
                }

                if (string.IsNullOrEmpty(request.PO_NO))
                {
                    return Ok(new { Status = "E", Message = "PO_NO is required" });
                }

                if (string.IsNullOrEmpty(request.DELV_DATE))
                {
                    return Ok(new { Status = "E", Message = "DELV_DATE is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_DD_UPD_RFC");

                myfun.SetValue("PO_NO", request.PO_NO);
                myfun.SetValue("DELV_DATE", request.DELV_DATE);

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

    public class ZPO_DD_UPD_Request
    {
        public string PO_NO { get; set; }
        public string DELV_DATE { get; set; }
    }
}