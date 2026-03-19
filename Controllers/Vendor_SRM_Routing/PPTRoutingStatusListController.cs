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
    public class PPTRoutingStatusListController : BaseController
    {
        public async Task<HttpResponseMessage> POST(string pptno)
        {
           // RoutingStatusListRequest request = new RoutingStatusListRequest();

            RoutingStatusList Authenticate = new RoutingStatusList();
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
                        myfun = rfcrep.CreateFunction("ZRFC_PPT_GET_ROUT"); //RfcFunctionName
                                                                            //myfun = rfcrep.CreateFunction("ZSRM_PO_GROUP_ROUTING"); //RfcFunctionName
                                                                            //  myfun.SetValue("IM_PO_NO", request.IM_PO); //Import Parameter
                                                                            //myfun.SetValue("IM_DESIGN", request.IM_DESIGN); //Import Parameter
                                                                            //myfun.SetValue("IM_SATNR", request.Article_Number); //Import Parameter

                        //myfun = rfcrep.CreateFunction("ZSRM_ROUTING_STATUS"); //RfcFunctionName
                        myfun.SetValue("IM_PPT_NO", pptno); //Import Parameter
                        //myfun.SetValue("IM_DESIGN", request.IM_DESIGN); //Import Parameter
                        //myfun.SetValue("IM_SATNR", request.Article_Number); //Import Parameter

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
                            RoutingStatusListResponse routingStatusListResponsegrp = new RoutingStatusListResponse();
                            if (IrfTable.RowCount > 0)
                            {
                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable[i].GetString("RTNO");

                                    Authenticate.Data.Add(authenticateResponse);
                                }




                                Authenticate.Status = true;
                                Authenticate.Message = "Routing Status List fetch Successfully";
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