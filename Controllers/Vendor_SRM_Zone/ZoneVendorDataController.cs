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
    public class ZoneVendorDataController : BaseController
    {

        public async Task<HttpResponseMessage> POST([FromUri] string zoneId)
        {
            Authenticate<ZoneAuthenticateResponse> Authenticate = new Authenticate<ZoneAuthenticateResponse>();

            try
            {

                if (zoneId != "" && zoneId != "")
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZSRM_GET_VENDOR_ZONE_DATA"); //RfcFunctionName
                        myfun.SetValue("IM_ZONE_ID", zoneId); //Import Parameter
                        myfun.Invoke(dest);

                        IRfcTable IrfTable = myfun.GetTable("ET_DATA");
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
                            for (int i = 0; i < IrfTable.RowCount; ++i)
                            {
                                ZoneAuthenticateResponse authenticateResponse = new ZoneAuthenticateResponse();
                                authenticateResponse.Account_Number = IrfTable[i].GetString("LIFNR");
                                authenticateResponse.Vendor_Name1 = IrfTable[i].GetString("NAME1");
                                authenticateResponse.ZoneId = IrfTable[i].GetString("ZONE_ID");
                                authenticateResponse.ZoneState = IrfTable[i].GetString("STATE");
                                authenticateResponse.Vendor_Name = IrfTable[i].GetString("TA_NAME");

                                Authenticate.Data.Add(authenticateResponse);
                            }
                            Authenticate.Data = Authenticate.Data.GroupBy(o => o.Account_Number).Select(o => o.FirstOrDefault()).ToList();//istinct(row).ToList();
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
