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
        public IHttpActionResult ExecuteZPO_COST_UPD_RFC([FromBody] ZPO_COST_UPD_RFCRequest request)
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

                IRfcStructure imDataStructure = myfun.GetStructure("IM_DATA");
                
                // Map request data to SAP structure
                if (!string.IsNullOrEmpty(request.IM_DATA.PO_NUMBER))
                    imDataStructure.SetValue("PO_NUMBER", request.IM_DATA.PO_NUMBER);
                if (!string.IsNullOrEmpty(request.IM_DATA.PLANT))
                    imDataStructure.SetValue("PLANT", request.IM_DATA.PLANT);
                if (!string.IsNullOrEmpty(request.IM_DATA.MATERIAL))
                    imDataStructure.SetValue("MATERIAL", request.IM_DATA.MATERIAL);
                if (!string.IsNullOrEmpty(request.IM_DATA.COST))
                    imDataStructure.SetValue("COST", request.IM_DATA.COST);
                if (!string.IsNullOrEmpty(request.IM_DATA.CURRENCY))
                    imDataStructure.SetValue("CURRENCY", request.IM_DATA.CURRENCY);
                if (!string.IsNullOrEmpty(request.IM_DATA.VENDOR))
                    imDataStructure.SetValue("VENDOR", request.IM_DATA.VENDOR);

                myfun.SetValue("IM_DATA", imDataStructure);

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

    public class ZPO_COST_UPD_RFCRequest
    {
        public ZST_PO_IMP IM_DATA { get; set; }
    }

    public class ZST_PO_IMP
    {
        public string PO_NUMBER { get; set; }
        public string PLANT { get; set; }
        public string MATERIAL { get; set; }
        public string COST { get; set; }
        public string CURRENCY { get; set; }
        public string VENDOR { get; set; }
    }
}