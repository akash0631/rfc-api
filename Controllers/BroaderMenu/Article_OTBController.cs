using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Vml;
using DocumentFormat.OpenXml.Vml.Office;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Ajax.Utilities;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Windows.Media.Media3D;
using Vendor_Application_MVC.Models;

namespace Vendor_Application_MVC.Controllers
{
    public class Article_OTBController : BaseController
    {

        public async Task<HttpResponseMessage> POST(Article_OTB_request request)
        {


            //return await Task.Run(() =>
            //{
            Article_OTB Authenticate = new Article_OTB();
            try
            {


                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZPLC_PO_RFC"); //RfcFunctionName
                    myfun.SetValue("IM_MATNR_FROM", request.articleno); //Import Parameter
                                                                        //Import Parameter
                    myfun.Invoke(dest);


                    IRfcTable IrfTable = myfun.GetTable("TT_DATA");
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
                                Article_OTB_response gateEntryResponse = new Article_OTB_response();

                                gateEntryResponse.Material_Group = IrfTable[i].GetString("MATKL");
                                gateEntryResponse.Segment = IrfTable[i].GetString("SEG");
                                gateEntryResponse.Division = IrfTable[i].GetString("DIVISION");
                                gateEntryResponse.Sub_Division = IrfTable[i].GetString("SUB_DIVISION");
                                gateEntryResponse.Major_Category
 = IrfTable[i].GetString("MAJ_CAT");
                                gateEntryResponse.Major_Category_Status
 = IrfTable[i].GetString("MAJ_CAT_STAT");
                                gateEntryResponse.Sub_Category_Description
 = IrfTable[i].GetString("SUB_CAT_DESC");
                                gateEntryResponse.MC_Description
 = IrfTable[i].GetString("MC_DESC");
                                gateEntryResponse.Season
 = IrfTable[i].GetString("SEASON");
                                gateEntryResponse.MVGR
 = IrfTable[i].GetString("ATNAM");
                                gateEntryResponse.MVGR_Value
 = IrfTable[i].GetString("ATWRT");
                                gateEntryResponse.PO_RAISING_MONTH
 = IrfTable[i].GetString("ZMONTH");
                                gateEntryResponse.max_opt_in_maj_cat
 = IrfTable[i].GetString("MAX_OPT_IN_MAJ_CAT");
                                gateEntryResponse.AUTO_CONT
 = IrfTable[i].GetString("AUTOCONT");
                                gateEntryResponse.BGT_CONT
 = IrfTable[i].GetString("BGT_CONT");
                                gateEntryResponse.Year
 = IrfTable[i].GetString("ZYEAR");
                                gateEntryResponse.BGT_OPT_CNT
 = IrfTable[i].GetString("BGT_OPT_CNT");
                                gateEntryResponse.BGT_PO_QTY
 = IrfTable[i].GetString("BGT_PR_Q");
                                gateEntryResponse.BGT_PO_VALUE
 = IrfTable[i].GetString("BGT_PR_V");
                                gateEntryResponse.MVGR_COUNT
 = IrfTable[i].GetString("MVGR_COUNT");
                                gateEntryResponse.MVGR_OPT_COUNT
 = IrfTable[i].GetString("MVGR_OPT_COUNT");
                                gateEntryResponse.TAG_QTY
 = IrfTable[i].GetString("TAG_QTY");
                                gateEntryResponse.PO_QTY
 = IrfTable[i].GetString("PO_QTY");
                                gateEntryResponse.PO_VAL
 = IrfTable[i].GetString("PO_VAL");
                                gateEntryResponse.OPT_BAL
 = IrfTable[i].GetString("OPT_BAL");
                                gateEntryResponse.QTY_OTB
 = IrfTable[i].GetString("QTY_OTB");
                                gateEntryResponse.VAL_OTB
 = IrfTable[i].GetString("VAL_OTB");
                                gateEntryResponse.TAG_VAL
 = IrfTable[i].GetString("TAG_VAL");



                                Authenticate.Data.Add(gateEntryResponse);
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
            //});

        }
    }
    public class Article_OTB
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<Article_OTB_response> Data;
        public Article_OTB()
        {
            Data = new List<Article_OTB_response>();

        }
    }
    public class Article_OTB_response
    {
        public string articleno { get; set; }
        public string Material_Group { get; set; }
        public string Segment { get; set; }
        public string Division { get; set; }
        public string Sub_Division { get; set; }
        public string Major_Category { get; set; }
        public string Major_Category_Status { get; set; }
        public string Sub_Category_Description { get; set; }
        public string MC_Description { get; set; }
        public string Season { get; set; }
        public string MVGR { get; set; }
        public string MVGR_Value { get; set; }
        public string PO_RAISING_MONTH { get; set; }
        public string max_opt_in_maj_cat { get; set; }
        public string AUTO_CONT { get; set; }
        public string BGT_CONT { get; set; }
        public string Year { get; set; }
        public string BGT_OPT_CNT { get; set; }
        public string BGT_PO_QTY { get; set; }
        public string BGT_PO_VALUE { get; set; }
        public string MVGR_COUNT { get; set; }
        public string MVGR_OPT_COUNT { get; set; }
        public string TAG_QTY { get; set; }
        public string PO_QTY { get; set; }
        public string PO_VAL { get; set; }
        public string OPT_BAL { get; set; }
        public string QTY_OTB { get; set; }
        public string VAL_OTB { get; set; }
        public string TAG_VAL { get; set; }



    }
    public class Article_OTB_request
    {
        public string articleno { get; set; }


    }

}