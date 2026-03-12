using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Models;
using System.Threading.Tasks;
using System.Configuration;
using Newtonsoft.Json.Linq;
using DocumentFormat.OpenXml.Office2016.Excel;
using System.Web.Http.Cors;
using System.IO;
using System.Threading.Tasks;
using Vendor_SRM_Routing_Application.Utils.Logger;
using Newtonsoft.Json;
namespace Vendor_Application_MVC.Controllers
{
    public class PO_COMP_POSTController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post(List<PO_COMP_Request> request)
        {

            PO_COMP_Status PO_Detail = new PO_COMP_Status();
            try
            {


                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                // Get RfcTable from SAP
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = null;
                myfun = rfcrep.CreateFunction("ZSRM_PO_COMP_POST"); //RfcFunctionName
                IRfcTable E_Data = myfun.GetTable("IT_DATA");
                foreach (var k in request)
                {
                    E_Data.Append();
                    E_Data.SetValue("MANDT", k.MANDT);
                    E_Data.SetValue("EBELN", k.EBELN);
                    E_Data.SetValue("SNO", k.SNO);
                    E_Data.SetValue("VALUE", k.VALUE);
                }
                //E_Data.SetValue("VENDOR_CRM", request.VENDOR_CRM);
                //E_Data.SetValue("COLOR_SD", request.COLOR_SD);
                //E_Data.SetValue("PACKING_NCV", request.PACKING_NCV);
                //E_Data.SetValue("PACK_SPP", request.PACK_SPP);
                //E_Data.SetValue("TAFETA_B", request.TAFETA_B);
                //E_Data.SetValue("BARCODING_V", request.BARCODING_V);
                //E_Data.SetValue("BARCODE_TKA", request.BARCODE_TKA);
                //E_Data.SetValue("MIN_SED", request.MIN_SED);
                //E_Data.SetValue("PRIVATE_L", request.PRIVATE_L);
                //E_Data.SetValue("NAME_PPL", request.NAME_PPL);
                //E_Data.SetValue("STANDARD_C", request.STANDARD_C);
                //E_Data.SetValue("PLANNED_B", request.PLANNED_B);
                //E_Data.SetValue("SEALED_S", request.SEALED_S);
                //E_Data.SetValue("PPT_TYPE", request.PPT_TYPE);

                myfun.Invoke(dest);
                IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
                string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                if (SAP_TYPE == "E")
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "" + SAP_Message + "";

                    return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);

                }
                else
                {

                    PO_Detail.Status = true;
                    PO_Detail.Message = "" + SAP_Message + "";

                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);

                }

            }
            catch (Exception ex)
            {

                PO_Detail.Status = false;
                PO_Detail.Message = "" + ex.Message + "";

                return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);

            }



        }


    }
}