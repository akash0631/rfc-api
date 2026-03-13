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
    public class ConfigParametersController : BaseController
    {
        [HttpPost]
        [Route("api/configparameters")]
        public async Task<IHttpActionResult> GetConfigParameters()
        {
            try
            {
                var destination = RfcDestinationManager.GetDestination("SAP_SYSTEM");
                var function = destination.Repository.CreateFunction("rfcConfigparameters");

                function.Invoke(destination);

                var exReturn = function.GetStructure("EX_RETURN");
                
                if (exReturn["TYPE"].ToString() == "E")
                {
                    return Ok(new ConfigParametersResponse
                    {
                        Status = "E",
                        Message = exReturn["MESSAGE"].ToString()
                    });
                }

                return Ok(new ConfigParametersResponse
                {
                    Status = "S",
                    Message = exReturn["MESSAGE"].ToString()
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new ConfigParametersResponse
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new ConfigParametersResponse
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new ConfigParametersResponse
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }

    public class ConfigParametersResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
    }
}