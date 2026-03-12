using DocumentFormat.OpenXml.Bibliography;
using OfficeOpenXml.Table.PivotTable;
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
    public class Store_Site_Creation_NewController : BaseController
    {
        public async Task<HttpResponseMessage> POST(Store_Site_CreationRequest request)
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
                        myfun = rfcrep.CreateFunction("ZSITE_RFC_CREATE"); //RfcFunctionName
                        IRfcStructure E_Data = myfun.GetStructure("IM_DATA");
                        E_Data.SetValue("RM_NAME", request.RM_NAME);
                        E_Data.SetValue("ZONE_1", request.ZONE_1);
                        E_Data.SetValue("ZSTATE", request.ZSTATE);
                        E_Data.SetValue("DISTRICT_NAME", request.DISTRICT_NAME);
                        E_Data.SetValue("CITY", request.CITY);
                        E_Data.SetValue("CITY_POPULATION", request.CITY_POPULATION);
                        E_Data.SetValue("C_B_PASS", request.C_B_PASS);
                        E_Data.SetValue("B_PASS", request.B_PASS);
                        E_Data.SetValue("F_PASS", request.F_PASS);
                        E_Data.SetValue("LLRATE", request.LLRATE);
                        E_Data.SetValue("VRATE", request.VRATE);
                        E_Data.SetValue("RANK", request.RANK);
                        E_Data.SetValue("SITE_TYPE", request.SITE_TYPE);
                        E_Data.SetValue("MRKT_NAME", request.MRKT_NAME);
                        E_Data.SetValue("FRONTAGE", request.FRONTAGE);
                        E_Data.SetValue("TOTAL_AREA", request.TOTAL_AREA);
                        E_Data.SetValue("BSMT_PRKG", request.BSMT_PRKG);
                        E_Data.SetValue("FRONT_PRKG", request.FRONT_PRKG);
                        E_Data.SetValue("UGF", request.UGF);
                        E_Data.SetValue("LGF", request.LGF);
                        E_Data.SetValue("GROUND_FLOOR", request.GROUND_FLOOR);
                        E_Data.SetValue("FIRST_FLOOR", request.FIRST_FLOOR);
                        E_Data.SetValue("SECOND_FLOOR", request.SECOND_FLOOR);
                        E_Data.SetValue("THIRD_FLOOR", request.THIRD_FLOOR);
                        E_Data.SetValue("FORTH_FLOOR", request.FORTH_FLOOR);
                        E_Data.SetValue("FIFTH_FLOOR", request.FIFTH_FLOOR);
                        E_Data.SetValue("GOOGLE_COORDINATES", request.GOOGLE_COORDINATES);
                        E_Data.SetValue("COMPETITORS_NAME", request.COMPETITORS_NAME);
                        E_Data.SetValue("COMPETITORS_SALE", request.COMPETITORS_SALE);
                        E_Data.SetValue("REMARKS", request.REMARKS);
                        E_Data.SetValue("REMARKS1", request.REMARKS1);
                        E_Data.SetValue("REMARKS2", request.REMARKS2);
                        E_Data.SetValue("BROKER_NAME", request.BROKER_NAME);
                        E_Data.SetValue("BROKERM_NO", request.BROKERM_NO);
                        E_Data.SetValue("LANDLORD_NAME", request.LANDLORD_NAME);
                        E_Data.SetValue("LANDLORD_M_NO", request.LANDLORD_M_NO);
                        E_Data.SetValue("PROOF", request.PROOF);

                        //added by Ishu Miglani on 16 dec 2024 04:35 pm
                        E_Data.SetValue("ROAD_CONDITION", request.ROAD_CONDITION);
                        E_Data.SetValue("ROAD_WIDTH", request.ROAD_WIDTH);
                        E_Data.SetValue("PROPERTY_CEILING_HIGHT", request.PROPERTY_CEILING_HIGHT);
                        myfun.Invoke(dest);


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