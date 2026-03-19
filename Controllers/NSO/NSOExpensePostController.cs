using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Vml.Office;
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
    public class NSOExpensePostController : BaseController
    {

        public async Task<HttpResponseMessage> POST(NSOExpensePost_request request)
        {


            //return await Task.Run(() =>
            //{
            NSOExpensePost Authenticate = new NSOExpensePost();
            try
            {


                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZRFC_ACC_DOC_POST"); //RfcFunctionName
                    //myfun.SetValue("IM_DC_ROUTING", request.IM_ROUTING_NO); //Import Parameter
                    //myfun.SetValue("IM_GATE_ENTRY", request.IM_GATE_ENTRY);
                    IRfcStructure IrfTable1 = myfun.GetStructure("LS_HEADER");
                    IrfTable1.SetValue("USERNAME", request.NSOExpensePost_LS_GET_DATA.USERNAME);
                    IrfTable1.SetValue("HEADER_TXT", request.NSOExpensePost_LS_GET_DATA.HEADER_TXT);
                    IrfTable1.SetValue("DOC_DATE", request.NSOExpensePost_LS_GET_DATA.DOC_DATE);
                    IrfTable1.SetValue("PSTNG_DATE", request.NSOExpensePost_LS_GET_DATA.PSTNG_DATE);
                    IrfTable1.SetValue("TRANS_DATE", request.NSOExpensePost_LS_GET_DATA.TRANS_DATE);
                    IrfTable1.SetValue("FISC_YEAR", request.NSOExpensePost_LS_GET_DATA.FISC_YEAR);
                    IrfTable1.SetValue("FIS_PERIOD", request.NSOExpensePost_LS_GET_DATA.FIS_PERIOD);
                    IrfTable1.SetValue("REF_DOC_NO", request.NSOExpensePost_LS_GET_DATA.REF_DOC_NO);
                    IRfcTable IrfTable2 = myfun.GetTable("LT_ACCOUNTGL");
                    foreach (var k in request.NSOExpensePost_LT_ACCOUNTGL)
                    {
                        IrfTable2.Append();
                        //IrfTable2.SetValue("ITEMNO_ACC", request.NSOExpensePost_LT_ACCOUNTGL.ITEMNO_ACC);

                        IrfTable2.SetValue("GL_ACCOUNT", k.GL_ACCOUNT);
                        IrfTable2.SetValue("COSTCENTER", k.COSTCENTER);
                        IrfTable2.SetValue("PROFIT_CTR", k.PROFIT_CTR);
                    }

                    IRfcTable IrfTable3 = myfun.GetTable("LT_CURRENCYAMOUNT");
                    foreach (var k in request.NSOExpensePost_LT_CURRENCYAMOUNT)
                    {
                        IrfTable3.Append();
                        //IrfTable3.SetValue("ITEMNO_ACC", request.NSOExpensePost_LT_CURRENCYAMOUNT.ITEMNO_ACC);
                        IrfTable3.SetValue("AMT_DOCCUR", k.AMT_DOCCUR);
                    }
                    IRfcTable IrfTable4 = myfun.GetTable("LT_PAYBLE");
                    IrfTable4.Append();
                    //IrfTable4.SetValue("ITEMNO_ACC", request.NSOExpensePost_LT_PAYBLE.ITEMNO_ACC);
                    IrfTable4.SetValue("VENDOR_NO", request.NSOExpensePost_LT_PAYBLE.VENDOR_NO);
                    IrfTable4.SetValue("BLINE_DATE", request.NSOExpensePost_LT_PAYBLE.BLINE_DATE);
                    IrfTable4.SetValue("ITEM_TEXT", request.NSOExpensePost_LT_PAYBLE.ITEM_TEXT);


                    myfun.Invoke(dest);


                    //IRfcTable IrfTable = myfun.GetTable("LT_DATA");
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
                        //if (IrfTable.RowCount > 0)
                        //{
                        //    for (int i = 0; i < IrfTable.RowCount; ++i)
                        //    {
                        //        ZDC_ROUTING_SUB_RFC_response gateEntryResponse = new ZDC_ROUTING_SUB_RFC_response();

                        //        gateEntryResponse.GE_NO = IrfTable[i].GetString("GE_NO");
                        //        gateEntryResponse.DCNO = IrfTable[i].GetString("DCNO");
                        //        gateEntryResponse.TEXT = IrfTable[i].GetString("TEXT");
                        //        gateEntryResponse.EMP_CD = IrfTable[i].GetString("EMP_CD");
                        //        gateEntryResponse.PO_NUM = IrfTable[i].GetString("PO_NUM");


                        //        Authenticate.Data.Add(gateEntryResponse);
                        //    }

                        //    Authenticate.Status = true;
                        //    Authenticate.Message = "Routing Status List fetch Successfully";
                        //    return Request.CreateResponse(HttpStatusCode.OK, Authenticate);

                        //}
                        //else
                        //{
                        //    Authenticate.Status = false;
                        //    Authenticate.Message = "No Data found";
                        //    return Request.CreateResponse(HttpStatusCode.NotFound, Authenticate);
                        //}
                        Authenticate.Status = true;
                        Authenticate.Message = "Routing Status List fetch Successfully";
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
            //});

        }
    }
    public class NSOExpensePost
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<NSOExpensePost_response> Data;
        public NSOExpensePost()
        {
            Data = new List<NSOExpensePost_response>();

        }
    }
    public class NSOExpensePost_response
    {
        public string GE_NO { get; set; }
        public string DCNO { get; set; } = string.Empty;
        public string TEXT { get; set; }
        public string EMP_CD { get; set; } = string.Empty;
        public string PO_NUM { get; set; }


    }
    public class NSOExpensePost_request
    {

        public NSOExpensePost_LS_GET_DATA NSOExpensePost_LS_GET_DATA { get; set; }
        public List<NSOExpensePost_LT_ACCOUNTGL> NSOExpensePost_LT_ACCOUNTGL { get; set; }
        public List<NSOExpensePost_LT_CURRENCYAMOUNT> NSOExpensePost_LT_CURRENCYAMOUNT { get; set; }
        public NSOExpensePost_LT_PAYBLE NSOExpensePost_LT_PAYBLE { get; set; }

    }
    public class NSOExpensePost_LS_GET_DATA
    {

        public string USERNAME { get; set; }
        public string HEADER_TXT { get; set; }
        public string DOC_DATE { get; set; }
        public string PSTNG_DATE { get; set; }
        public string TRANS_DATE { get; set; }
        public string FISC_YEAR { get; set; }
        public string FIS_PERIOD { get; set; }
        public string REF_DOC_NO { get; set; }

    }
    public class NSOExpensePost_LT_ACCOUNTGL
    {

        //public string ITEMNO_ACC { get; set; }
        public string GL_ACCOUNT { get; set; }
        public string COSTCENTER { get; set; }
        public string PROFIT_CTR { get; set; }


    }

    public class NSOExpensePost_LT_CURRENCYAMOUNT
    {

        //public string ITEMNO_ACC { get; set; }
        public string AMT_DOCCUR { get; set; }



    }
    public class NSOExpensePost_LT_PAYBLE
    {

        //public string ITEMNO_ACC { get; set; }
        public string VENDOR_NO { get; set; }
        public string BLINE_DATE { get; set; }
        public string ITEM_TEXT { get; set; }



    }

}