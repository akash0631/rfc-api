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
    public class ZonePODataController : BaseController
    {

        public async Task<HttpResponseMessage> POST([FromUri] string vendorCode)
        {
            Authenticate<ZoneVendorPoResponse> Authenticate = new Authenticate<ZoneVendorPoResponse>();

            try
            {

                if (vendorCode != "" && vendorCode != "")
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZSRM_VEND_PEND_PO"); //RfcFunctionName
                        myfun.SetValue("IM_LIFNR", vendorCode); //Import Parameter
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
                                ZoneVendorPoResponse authenticateResponse = new ZoneVendorPoResponse();
                                authenticateResponse.Vendor_Code = IrfTable[i].GetString("LIFNR");
                                authenticateResponse.Vendor_Name = IrfTable[i].GetString("NAME1");
                                authenticateResponse.PO_Number = IrfTable[i].GetString("EBELN");
                                authenticateResponse.EBelp = IrfTable[i].GetString("EBELP");
                                authenticateResponse.Menge = IrfTable[i].GetString("MENGE");

                                Authenticate.Data.Add(authenticateResponse);
                            }
                            Authenticate.Data = Authenticate.Data.GroupBy(o => o.PO_Number).Select(o => o.FirstOrDefault()).ToList();//istinct(row).ToList();
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
