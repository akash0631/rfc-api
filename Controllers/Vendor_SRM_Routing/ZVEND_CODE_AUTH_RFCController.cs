using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using VendorSrmRoutingApplication.Controllers;

namespace VendorSrmRoutingApplication.Controllers
{
    public class ZVEND_CODE_AUTH_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVEND_CODE_AUTH_RFC")]
        public HttpResponseMessage ZVEND_CODE_AUTH_RFC([FromBody] ZVEND_CODE_AUTH_RFCRequest request)
        {
            IRfcConnection connection = null;
            
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = "Request cannot be null"
                    });
                }

                if (string.IsNullOrEmpty(request.IM_VEND_ID))
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = "IM_VEND_ID is required"
                    });
                }

                if (string.IsNullOrEmpty(request.IM_PASSWORD))
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = "IM_PASSWORD is required"
                    });
                }

                connection = SapRfcConnection.GetConnection("production");
                IRfcFunction myfun = connection.CreateFunction("ZVEND_CODE_AUTH_RFC");

                myfun.SetValue("IM_VEND_ID", request.IM_VEND_ID);
                myfun.SetValue("IM_PASSWORD", request.IM_PASSWORD);

                myfun.Invoke(connection);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = returnMessage
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = returnMessage
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            finally
            {
                if (connection != null)
                {
                    try
                    {
                        connection.Dispose();
                    }
                    catch { }
                }
            }
        }
    }

    public class ZVEND_CODE_AUTH_RFCRequest
    {
        public string IM_VEND_ID { get; set; }
        public string IM_PASSWORD { get; set; }
    }
}
