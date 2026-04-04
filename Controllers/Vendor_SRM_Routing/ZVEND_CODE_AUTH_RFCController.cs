using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace VendorSrmRoutingApplication.Controllers
{
    public class ZVEND_CODE_AUTH_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVEND_CODE_AUTH_RFC")]
        public IHttpActionResult VendorCodeAuth([FromBody] VendorAuthRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new { Status = "E", Message = "Request body cannot be null" });
                }

                if (string.IsNullOrEmpty(request.IM_VEND_ID))
                {
                    return Ok(new { Status = "E", Message = "IM_VEND_ID is required" });
                }

                if (string.IsNullOrEmpty(request.IM_PASSWORD))
                {
                    return Ok(new { Status = "E", Message = "IM_PASSWORD is required" });
                }

                RfcDestination connection = null;
                try
                {
                    connection = SapRfcConnection.GetConnection("production");
                    RfcRepository rfcrep = connection.Repository;
                    IRfcFunction myfun = rfcrep.CreateFunction("ZVEND_CODE_AUTH_RFC");

                    myfun.SetValue("IM_VEND_ID", request.IM_VEND_ID);
                    myfun.SetValue("IM_PASSWORD", request.IM_PASSWORD);

                    myfun.Invoke(connection);

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
            finally
            {
                if (connection != null)
                {
                    connection.Dispose();
                }
            }
        }
    }

    public class VendorAuthRequest
    {
        public string IM_VEND_ID { get; set; }
        public string IM_PASSWORD { get; set; }
    }
}