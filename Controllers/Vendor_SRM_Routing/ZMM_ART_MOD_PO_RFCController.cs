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
    [RoutePrefix("api")]
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("ZMM_ART_MOD_PO_RFC")]
        public async Task<IHttpActionResult> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
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
                    SetIMInputStructure(imInputStruct, request.IM_INPUT);
                    myfun.SetValue("IM_INPUT", imInputStruct);
                }

                if (request.IM_OUTPUT != null)
                {
                    IRfcStructure imOutputStruct = myfun.GetStructure("IM_OUTPUT");
                    SetIMOutputStructure(imOutputStruct, request.IM_OUTPUT);
                    myfun.SetValue("IM_OUTPUT", imOutputStruct);
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

        private void SetIMInputStructure(IRfcStructure structure, IMInputModel model)
        {
            if (model == null) return;

            foreach (var field in structure)
            {
                var property = typeof(IMInputModel).GetProperty(field.Metadata.Name);
                if (property != null)
                {
                    var value = property.GetValue(model);
                    if (value != null)
                    {
                        structure.SetValue(field.Metadata.Name, value);
                    }
                }
            }
        }

        private void SetIMOutputStructure(IRfcStructure structure, IMOutputModel model)
        {
            if (model == null) return;

            foreach (var field in structure)
            {
                var property = typeof(IMOutputModel).GetProperty(field.Metadata.Name);
                if (property != null)
                {
                    var value = property.GetValue(model);
                    if (value != null)
                    {
                        structure.SetValue(field.Metadata.Name, value);
                    }
                }
            }
        }
    }

    public class ZMM_ART_MOD_PO_RFCRequest
    {
        public IMInputModel IM_INPUT { get; set; }
        public IMOutputModel IM_OUTPUT { get; set; }
    }

    public class IMInputModel
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string EINDT { get; set; }
        public string UEBTO { get; set; }
        public string UEBTK { get; set; }
    }

    public class IMOutputModel
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string EINDT { get; set; }
        public string MESSAGE { get; set; }
        public string TYPE { get; set; }
    }
}