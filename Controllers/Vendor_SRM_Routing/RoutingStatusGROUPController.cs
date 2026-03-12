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
    public class RoutingStatusGROUPController : BaseController
    {
        public async Task<HttpResponseMessage> POST(RoutingStatusListRequest request)
        {

            RoutingStatus_grp Authenticate = new RoutingStatus_grp();
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
                        myfun = rfcrep.CreateFunction("ZSRM_PO_GET_GROUT_STAT"); //RfcFunctionName
                        myfun.SetValue("IM_PO_NO", request.IM_PO); //Import Parameter
                                                                   //myfun.SetValue("IM_DESIGN", request.IM_DESIGN); //Import Parameter
                                                                   //myfun.SetValue("IM_SATNR", request.Article_Number); //Import Parameter

                        //myfun = rfcrep.CreateFunction("ZSRM_ROUTING_STATUS"); //RfcFunctionName
                        //myfun.SetValue("IM_PO", request.IM_PO); //Import Parameter
                        //myfun.SetValue("IM_DESIGN", request.IM_DESIGN); //Import Parameter
                        //myfun.SetValue("IM_SATNR", request.Article_Number); //Import Parameter

                        myfun.Invoke(dest);


                        IRfcTable IrfTable = myfun.GetTable("ET_GROUT_STAT");
                        //IRfcTable IrfTable = myfun.GetTable("ET_PRD_ROUTING");
                        //IRfcTable IrfTable1 = myfun.GetTable("ET_BR");
                        //IRfcTable IrfTable2 = myfun.GetTable("ET_ACCST");
                        //IRfcTable IrfTable3 = myfun.GetTable("ET_PPSR");
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
                            // RoutingStatusgrpRespponse routingStatusListResponsegrp = new RoutingStatusgrpRespponse();
                            if (IrfTable.RowCount > 0)
                            {
                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    RoutingStatusgrpRespponse authenticateResponse = new RoutingStatusgrpRespponse();
                                    authenticateResponse.TEXT = IrfTable[i].GetString("TEXT");
                                    authenticateResponse.RTNO = IrfTable[i].GetString("RTNO");
                                    authenticateResponse.SUB_GROUP = IrfTable[i].GetString("SUB_GROUP");

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