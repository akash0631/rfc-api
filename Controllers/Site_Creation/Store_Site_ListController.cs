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
    public class Store_Site_ListController : BaseController
    {
        public async Task<HttpResponseMessage> POST(Store_Creation_ListRequest request)
        {

            Store_Creation_List Authenticate = new Store_Creation_List();
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
                        myfun = rfcrep.CreateFunction("ZWM_STORE_SITE_CONF"); //RfcFunctionName
                        myfun.SetValue("IM_SITE_CODE", request.IM_SITE_CODE); //Import Parameter

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
                            if (IrfTable.RowCount > 0)
                            {
                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    Store_Creation_ListResponse authenticateResponse = new Store_Creation_ListResponse();
                                    authenticateResponse.SRNO = IrfTable[i].GetString("SRNO");
                                    authenticateResponse.TEXT = IrfTable[i].GetString("TEXT");

                                    Authenticate.Data.Add(authenticateResponse);
                                }

                                Authenticate.Status = true;
                                Authenticate.Message = "List fetch Successfully";
                                return Request.CreateResponse(HttpStatusCode.OK, Authenticate);

                            }
                            else
                            {
                                Authenticate.Status = false;
                                Authenticate.Message = "No Data found";
                                return Request.CreateResponse(HttpStatusCode.NotFound, Authenticate);
                            }
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