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
                if (request == null)
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = "Request data is required"
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZPO_COST_UPD_RFC");

                IRfcStructure imData = myfun.GetStructure("IM_DATA");
                if (request.IM_DATA != null)
                {
                    foreach (var property in request.IM_DATA.GetType().GetProperties())
                    {
                        var value = property.GetValue(request.IM_DATA);
                        if (value != null)
                        {
                            imData.SetValue(property.Name, value);
                        }
                    }
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
                    Status = returnType,
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
            catch (RfcCommunicationException ex)
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

    public class ZPO_COST_UPD_RFCRequest
    {
        public ZST_PO_IMP IM_DATA { get; set; }
    }

    public class ZST_PO_IMP
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public decimal NETPR { get; set; }
        public string WAERS { get; set; }
        public string PEINH { get; set; }
        public string BPRME { get; set; }
    }
}