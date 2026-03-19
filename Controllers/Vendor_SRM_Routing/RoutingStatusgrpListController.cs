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
    public class RoutingStatusgrpListController : BaseController
    {
        public async Task<HttpResponseMessage> POST(RoutingStatusListRequest request)
        {

            RoutingStatusList_grp Authenticate = new RoutingStatusList_grp();
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
                        // myfun = rfcrep.CreateFunction("ZSRM_PO_RFC_GET_ROUTING"); //RfcFunctionName
                        myfun = rfcrep.CreateFunction("ZSRM_PO_GROUP_ROUTING"); //RfcFunctionName
                        myfun.SetValue("IM_PO_NO", request.IM_PO); //Import Parameter
                                                                   //myfun.SetValue("IM_DESIGN", request.IM_DESIGN); //Import Parameter
                                                                   //myfun.SetValue("IM_SATNR", request.Article_Number); //Import Parameter

                        //myfun = rfcrep.CreateFunction("ZSRM_ROUTING_STATUS"); //RfcFunctionName
                        //myfun.SetValue("IM_PO", request.IM_PO); //Import Parameter
                        //myfun.SetValue("IM_DESIGN", request.IM_DESIGN); //Import Parameter
                        //myfun.SetValue("IM_SATNR", request.Article_Number); //Import Parameter

                        myfun.Invoke(dest);


                        //IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                        IRfcTable IrfTable = myfun.GetTable("ET_PRD_ROUTING");
                        IRfcTable IrfTable1 = myfun.GetTable("ET_BR");
                        IRfcTable IrfTable2 = myfun.GetTable("ET_ACCST");
                        IRfcTable IrfTable3 = myfun.GetTable("ET_PPSR");

                        IRfcTable IrfTable4 = myfun.GetTable("ET_AMS");
                        IRfcTable IrfTable5 = myfun.GetTable("ET_FTA");
                        IRfcTable IrfTable6 = myfun.GetTable("ET_POSR");
                        IRfcTable IrfTable7 = myfun.GetTable("ET_TPM");

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
                            RoutingStatusListResponsegrp routingStatusListResponsegrp = new RoutingStatusListResponsegrp();
                            if (IrfTable.RowCount > 0)
                            {
                                for (int i = 0; i < IrfTable7.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable7[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable7[i].GetString("RTNO");

                                    routingStatusListResponsegrp.TK_PK_MVT.Add(authenticateResponse);
                                }

                                for (int i = 0; i < IrfTable6.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable6[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable6[i].GetString("RTNO");

                                    routingStatusListResponsegrp.PO_Sample_Rout.Add(authenticateResponse);
                                }
                                for (int i = 0; i < IrfTable5.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable5[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable5[i].GetString("RTNO");

                                    routingStatusListResponsegrp.Fabric_TNA.Add(authenticateResponse);
                                }

                                for (int i = 0; i < IrfTable4.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable4[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable4[i].GetString("RTNO");

                                    routingStatusListResponsegrp.Auto_Marker_ST.Add(authenticateResponse);
                                }




                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable[i].GetString("RTNO");

                                    routingStatusListResponsegrp.PRD_ROUTING.Add(authenticateResponse);
                                }

                                for (int i = 0; i < IrfTable1.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable1[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable1[i].GetString("RTNO");

                                    routingStatusListResponsegrp.Barcode_Routing.Add(authenticateResponse);
                                }

                                for (int i = 0; i < IrfTable2.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable2[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable2[i].GetString("RTNO");

                                    routingStatusListResponsegrp.ACC_Status.Add(authenticateResponse);
                                }

                                for (int i = 0; i < IrfTable3.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.TEXT = IrfTable3[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable3[i].GetString("RTNO");

                                    routingStatusListResponsegrp.PP_Sample_Rout.Add(authenticateResponse);
                                }

                                Authenticate.Data.Add(routingStatusListResponsegrp);
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