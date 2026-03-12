using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Vml.Office;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Models;

namespace Vendor_Application_MVC.Controllers
{
    public class ZWM_HU_STORE_TT_RFCController : BaseController
    {

        public async Task<HttpResponseMessage> POST(ZWM_HU_STORE_TT_request request)
        {


            //return await Task.Run(() =>
            //{
            ZWM_HU_STORE_TT Authenticate = new ZWM_HU_STORE_TT();
            try
            {


                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZWM_HU_STORE_POST_RFC"); //RfcFunctionName
                    myfun.SetValue("IM_USER", request.IM_USER); //Import Parameter
                    myfun.SetValue("IM_EXIDV", request.IM_EXIDV); //Import Parameter
                    myfun.SetValue("IM_SAPHU", request.IM_SAPHU); //Import Parameter

                   
                    
                    myfun.Invoke(dest);


                    //IRfcTable IrfTable = myfun.GetTable("LT_DATA");
                    IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                    if (SAP_TYPE == "E")
                    {
                        Authenticate.Status = false;
                        Authenticate.Message = "" + SAP_Message + "";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, Authenticate);
                    }
                    else
                    {
                       
                        Authenticate.Status = true;
                        Authenticate.Message = "" + SAP_Message + "";
                        return Request.CreateResponse(HttpStatusCode.OK, Authenticate);
                    }

                }
                catch (Exception ex)
                {
                    Authenticate.Status = false;
                    Authenticate.Message = "" + ex.Message + "";
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
                }

            }
            catch (Exception ex)
            {
                Authenticate.Status = false;
                Authenticate.Message = "" + ex.Message + "";
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
            }
            //});

        }
    }
    public class ZWM_HU_STORE_TT
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

       
    }
  
    public class ZWM_HU_STORE_TT_request
    {
        public string IM_USER { get; set; }
        public string IM_EXIDV { get; set; }
        public string IM_SAPHU { get; set; }

  
        

    }
   

   

}