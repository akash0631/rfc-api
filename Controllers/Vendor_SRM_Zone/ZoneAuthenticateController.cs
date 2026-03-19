using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.UI.WebControls;
using Vendor_Application_MVC.Models;

namespace Vendor_Application_MVC.Controllers.Vendor_SRM_Zonw
{
    public class ZoneAuthenticateController : BaseController
    {
        public async Task<HttpResponseMessage> POST([FromBody] AuthenticateRequest request)
        {
            Authenticate Authenticate = new Authenticate();

            try
            {
                if (request.Username != "" && request.Password != "" && request.Username != null && request.Password != null)
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZSRM_USER_AUTORITY_CHECK"); //RfcFunctionName
                        myfun.SetValue("IM_ZONE_ID", request.Username); //Import Parameter
                        myfun.SetValue("IM_PASSWORD", request.Password); //Import Parameter
                        myfun.Invoke(dest);

                        //IRfcTable IrfTable = myfun.GetTable("ET_DATA");
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
                else
                {
                    Authenticate.Status = false;
                    Authenticate.Message = "Username and Password is Mandatory.";
                    return Request.CreateResponse(HttpStatusCode.BadRequest, Authenticate);
                }
            }
            catch (Exception ex)
            {
                Authenticate.Status = false;
                Authenticate.Message = "" + ex.Message + "";
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
            }


        }
       
    }
}
