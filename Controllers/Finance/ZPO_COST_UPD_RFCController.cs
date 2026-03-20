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
        public IHttpActionResult ZPO_COST_UPD_RFC([FromBody] ZPO_COST_UPD_RFCRequest request)
        {
            try
            {
                if (request == null || request.IM_DATA == null)
                {
                    return Ok(new { Status = "E", Message = "Request data cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                IRfcStructure imDataStructure = myfun.GetStructure("IM_DATA");
                if (imDataStructure != null)
                {
                    foreach (var property in typeof(ZST_PO_IMP).GetProperties())
                    {
                        var value = property.GetValue(request.IM_DATA);
                        if (value != null)
                        {
                            imDataStructure.SetValue(property.Name, value);
                        }
                    }
                    myfun.SetValue("IM_DATA", imDataStructure);
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN != null)
                {
                    string returnType = EX_RETURN.GetString("TYPE");
                    string returnMessage = EX_RETURN.GetString("MESSAGE");

                    if (returnType == "E")
                    {
                        return Ok(new { Status = "E", Message = returnMessage });
                    }

                    return Ok(new { Status = "S", Message = returnMessage });
                }

                return Ok(new { Status = "S", Message = "Operation completed successfully" });
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
        public string PO_ITEM { get; set; }
        public decimal? COST_VALUE { get; set; }
        public string CURRENCY { get; set; }
        public string COST_TYPE { get; set; }
        public string PLANT { get; set; }
        public string VENDOR { get; set; }
        public string MATERIAL { get; set; }
        public DateTime? EFFECTIVE_DATE { get; set; }
        public string USER_ID { get; set; }
    }
}