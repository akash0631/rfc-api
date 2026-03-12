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

namespace Vendor_Application_MVC.Controllers
{
    public class Authenticate_sub_oldController : BaseController
    {
        // [HttpPost]
        // public void UploadFile()
        // {
        //     var file = HttpContext.Current.Request.Files.Count > 0 ?
        //HttpContext.Current.Request.Files[0] : null;
        // }

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
                        myfun = rfcrep.CreateFunction("ZSRM_SUBDIV_USER_VALIDATE"); //RfcFunctionName
                        myfun.SetValue("IM_USER", request.Username); //Import Parameter
                        myfun.SetValue("IM_PASSWORD", request.Password); //Import Parameter
                        myfun.Invoke(dest);


                        IRfcTable IrfTable = myfun.GetTable("ET_DATA");
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

                            for (int i = 0; i < IrfTable.RowCount; ++i)
                            {
                                AuthenticateResponse authenticateResponse = new AuthenticateResponse();
                                authenticateResponse.Vendor_Code = IrfTable[i].GetString("LIFNR");
                                authenticateResponse.Vendor_Name = IrfTable[i].GetString("NAME1");
                                authenticateResponse.PO_Number = IrfTable[i].GetString("EBELN");

                                Authenticate.Data.Add(authenticateResponse);
                            }
                            Authenticate.Data = Authenticate.Data.GroupBy(o => o.PO_Number).Select(o => o.FirstOrDefault()).ToList();
                            //Distinct(row).ToList();
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
