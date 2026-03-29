using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor
{
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public async Task<IHttpActionResult> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Content(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request body cannot be null" });
                }

                if (request.IM_INPUT == null)
                {
                    return Content(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_INPUT parameter is required" });
                }

                if (request.IM_OUTPUT == null)
                {
                    return Content(HttpStatusCode.BadRequest, new { Status = "E", Message = "IM_OUTPUT parameter is required" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                IRfcStructure IM_INPUT = myfun.GetStructure("IM_INPUT");
                if (!string.IsNullOrEmpty(request.IM_INPUT.ARTICLE))
                    IM_INPUT.SetValue("ARTICLE", request.IM_INPUT.ARTICLE);
                if (!string.IsNullOrEmpty(request.IM_INPUT.COLOR))
                    IM_INPUT.SetValue("COLOR", request.IM_INPUT.COLOR);
                if (!string.IsNullOrEmpty(request.IM_INPUT.PO_NUMBER))
                    IM_INPUT.SetValue("PO_NUMBER", request.IM_INPUT.PO_NUMBER);
                if (!string.IsNullOrEmpty(request.IM_INPUT.PLANT))
                    IM_INPUT.SetValue("PLANT", request.IM_INPUT.PLANT);
                if (!string.IsNullOrEmpty(request.IM_INPUT.VENDOR))
                    IM_INPUT.SetValue("VENDOR", request.IM_INPUT.VENDOR);

                IRfcStructure IM_OUTPUT = myfun.GetStructure("IM_OUTPUT");
                if (!string.IsNullOrEmpty(request.IM_OUTPUT.ARTICLE))
                    IM_OUTPUT.SetValue("ARTICLE", request.IM_OUTPUT.ARTICLE);
                if (!string.IsNullOrEmpty(request.IM_OUTPUT.COLOR))
                    IM_OUTPUT.SetValue("COLOR", request.IM_OUTPUT.COLOR);
                if (!string.IsNullOrEmpty(request.IM_OUTPUT.PO_NUMBER))
                    IM_OUTPUT.SetValue("PO_NUMBER", request.IM_OUTPUT.PO_NUMBER);
                if (!string.IsNullOrEmpty(request.IM_OUTPUT.PLANT))
                    IM_OUTPUT.SetValue("PLANT", request.IM_OUTPUT.PLANT);
                if (!string.IsNullOrEmpty(request.IM_OUTPUT.VENDOR))
                    IM_OUTPUT.SetValue("VENDOR", request.IM_OUTPUT.VENDOR);
                if (request.IM_OUTPUT.QUANTITY.HasValue)
                    IM_OUTPUT.SetValue("QUANTITY", request.IM_OUTPUT.QUANTITY.Value);
                if (request.IM_OUTPUT.PRICE.HasValue)
                    IM_OUTPUT.SetValue("PRICE", request.IM_OUTPUT.PRICE.Value);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE")?.ToString();
                string returnMessage = EX_RETURN.GetValue("MESSAGE")?.ToString();

                if (returnType == "E")
                {
                    return Content(HttpStatusCode.BadRequest, new { Status = "E", Message = returnMessage });
                }

                return Ok(new { Status = returnType, Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZMM_ART_MOD_PO_Request
    {
        public ZMM_PO_ART_ST IM_INPUT { get; set; }
        public ZMM_PO_ART_OUT_ST IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_ST
    {
        public string ARTICLE { get; set; }
        public string COLOR { get; set; }
        public string PO_NUMBER { get; set; }
        public string PLANT { get; set; }
        public string VENDOR { get; set; }
    }

    public class ZMM_PO_ART_OUT_ST
    {
        public string ARTICLE { get; set; }
        public string COLOR { get; set; }
        public string PO_NUMBER { get; set; }
        public string PLANT { get; set; }
        public string VENDOR { get; set; }
        public decimal? QUANTITY { get; set; }
        public decimal? PRICE { get; set; }
    }
}