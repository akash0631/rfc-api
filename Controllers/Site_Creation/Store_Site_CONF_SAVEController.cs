using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_Application_MVC.Models;
using Vendor_SRM_Routing_Application.Models.Site_Creation;

namespace Vendor_SRM_Routing_Application.Controllers.Site_Creation_Routing
{
    public class Store_Site_CONF_SAVEController : BaseController
    {
        public async Task<HttpResponseMessage> POST(Store_Creation_CONF_SAVERequest request)
        {

            Store_Creation_CONF_SAVE Authenticate = new Store_Creation_CONF_SAVE();
            return await Task.Run(() =>
            {
                try
                {


                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZWM_STORE_SITE_CONF_SAVE"); //RfcFunctionName
                        IRfcStructure E_Data = myfun.GetStructure("IM_DATA");
                        E_Data.SetValue("SRNO", request.SRNO);
                        E_Data.SetValue("PROC_DESC", request.PROC_DESC);
                        E_Data.SetValue("PROC_CONF", "F");
                        E_Data.SetValue("ACT_START_DATE", request.ACT_START_DATE);
                        E_Data.SetValue("ACT_END_DATE", request.ACT_END_DATE);
                        E_Data.SetValue("REMARK", request.REMARK);



                        myfun.Invoke(dest);


                        IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
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
            });


        }
    }
}