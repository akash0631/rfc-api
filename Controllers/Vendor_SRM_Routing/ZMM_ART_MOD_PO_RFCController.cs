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

                // Set IM_INPUT structure
                IRfcStructure imInputStructure = myfun.GetStructure("IM_INPUT");
                if (request.IM_INPUT != null)
                {
                    SetStructureValues(imInputStructure, request.IM_INPUT);
                }

                // Set IM_OUTPUT structure
                IRfcStructure imOutputStructure = myfun.GetStructure("IM_OUTPUT");
                if (request.IM_OUTPUT != null)
                {
                    SetStructureValues(imOutputStructure, request.IM_OUTPUT);
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Json(new { Status = "E", Message = returnMessage });
                }

                return Json(new { Status = "S", Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (CommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
        }

        private void SetStructureValues(IRfcStructure structure, dynamic inputObject)
        {
            if (inputObject == null) return;

            var properties = inputObject.GetType().GetProperties();
            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(inputObject);
                    if (value != null)
                    {
                        if (property.PropertyType == typeof(string))
                        {
                            structure.SetValue(property.Name, value.ToString());
                        }
                        else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                        {
                            structure.SetValue(property.Name, Convert.ToInt32(value));
                        }
                        else if (property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
                        {
                            structure.SetValue(property.Name, Convert.ToDecimal(value));
                        }
                        else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                        {
                            structure.SetValue(property.Name, Convert.ToDateTime(value));
                        }
                        else
                        {
                            structure.SetValue(property.Name, value.ToString());
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip properties that cannot be set
                    continue;
                }
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
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string EINDT { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
    }

    public class ZMM_PO_ART_OUT_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string MENGE { get; set; }
        public string NETPR { get; set; }
        public string EINDT { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}