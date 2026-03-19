using DocumentFormat.OpenXml.Vml;
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
    public class PPT_LISTController : BaseController
    {
        public async Task<HttpResponseMessage> POST()
            {
            // RoutingStatusListRequest request = new RoutingStatusListRequest();

            PPT_List Authenticate = new PPT_List();
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
                        myfun = rfcrep.CreateFunction("ZRFC_PPT_GET"); //RfcFunctionName
                                                                                  //myfun = rfcrep.CreateFunction("ZSRM_PO_GROUP_ROUTING"); //RfcFunctionName
                      //  myfun.SetValue("IM_PO_NO", request.IM_PO); //Import Parameter
                                                                   //myfun.SetValue("IM_DESIGN", request.IM_DESIGN); //Import Parameter
                                                                   //myfun.SetValue("IM_SATNR", request.Article_Number); //Import Parameter

                        //myfun = rfcrep.CreateFunction("ZSRM_ROUTING_STATUS"); //RfcFunctionName
                        //myfun.SetValue("IM_PO", request.IM_PO); //Import Parameter
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
                            PPT_ListResponse routingStatusListResponsegrp = new PPT_ListResponse();
                            if (IrfTable.RowCount > 0)
                            {
                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    PPT_ListResponse authenticateResponse = new PPT_ListResponse();
                                    authenticateResponse.PPT_NO = IrfTable[i].GetString("PPT_NO");
                                    authenticateResponse.Vendor_code = IrfTable[i].GetString("LIFNR");
                                    authenticateResponse.Vendor_Name = IrfTable[i].GetString("Name1");
                                    //authenticateResponse.Subdivision= IrfTable[i].GetString("PPT_DIV");
                                    authenticateResponse.Subdivision = IrfTable[i].GetString("Vtext");
                                    authenticateResponse.LRTNO_desc = IrfTable[i].GetString("LRTNO_desc");
                                    authenticateResponse.PPT_Creation_date = IrfTable[i].GetString("ERDAT");
                                    authenticateResponse.LRTNO = IrfTable[i].GetString("LRTNO");
                                    authenticateResponse.PPT_SHOW = IrfTable[i].GetString("PPT_SHOW");
                                    //authenticateResponse.PPT_SHOW = "0";



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

    public class PPT_List
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<PPT_ListResponse> Data;
        public PPT_List()
        {
            Data = new List<PPT_ListResponse>();

        }
    }
    public class PPT_ListResponse
    {
        public string PPT_NO { get; set; }
        public string Vendor_code { get; set; }
        public string Vendor_Name { get; set; }
        public string PPT_Creation_date { get; set; }

        public string Subdivision { get; set; }
        public string LRTNO_desc { get; set; }
        public string LRTNO { get; set; }
        public string PPT_SHOW { get; set; }





    }
}