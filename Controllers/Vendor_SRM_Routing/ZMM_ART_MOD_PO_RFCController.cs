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
        public IHttpActionResult ExecuteZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Request cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                if (request.IM_INPUT != null)
                {
                    IRfcStructure imInputStruct = myfun.GetStructure("IM_INPUT");
                    if (imInputStruct != null)
                    {
                        foreach (var property in typeof(ZMM_PO_ART_ST).GetProperties())
                        {
                            var value = property.GetValue(request.IM_INPUT);
                            if (value != null)
                            {
                                imInputStruct.SetValue(property.Name, value);
                            }
                        }
                    }
                }

                if (request.IM_OUTPUT != null)
                {
                    IRfcStructure imOutputStruct = myfun.GetStructure("IM_OUTPUT");
                    if (imOutputStruct != null)
                    {
                        foreach (var property in typeof(ZMM_PO_ART_OUT_ST).GetProperties())
                        {
                            var value = property.GetValue(request.IM_OUTPUT);
                            if (value != null)
                            {
                                imOutputStruct.SetValue(property.Name, value);
                            }
                        }
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string status = EX_RETURN.GetString("TYPE");
                string message = EX_RETURN.GetString("MESSAGE");

                if (status == "E")
                {
                    return Json(new { Status = "E", Message = message });
                }

                return Json(new { Status = status, Message = message });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
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
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string NETWR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string EINDT { get; set; }
    }

    public class ZMM_PO_ART_OUT_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal? MENGE { get; set; }
        public decimal? NETPR { get; set; }
        public string NETWR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string EINDT { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}