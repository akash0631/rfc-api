using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Vml.Office;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Models;

namespace Vendor_Application_MVC.Controllers
{
    public class ZDC_ROUTING_SUB_RFCController : BaseController
    {

        public async Task<HttpResponseMessage> POST(ZDC_ROUTING_SUB_RFC_request request)
        {


            //return await Task.Run(() =>
            //{
            ZDC_ROUTING_SUB_RFC Authenticate = new ZDC_ROUTING_SUB_RFC();
            try
            {


                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZDC_ROUTING_SUB_RFC"); //RfcFunctionName
                    myfun.SetValue("IM_DC_ROUTING", request.IM_ROUTING_NO); //Import Parameter
                    myfun.SetValue("IM_GATE_ENTRY", request.IM_GATE_ENTRY);
                    myfun.SetValue("IM_EBELN", request.IM_EBELN);
                    IRfcStructure IrfTable1 = myfun.GetStructure("LS_GET_DATA");

                    IrfTable1.SetValue("BGT_START_DATE", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.BGT_START_DATE.ToString("yyyyMMdd"));
                    IrfTable1.SetValue("BGT_END_DATE", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.BGT_END_DATE.ToString("yyyyMMdd"));
                    IrfTable1.SetValue("ACT_START_DATE", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.ACT_START_DATE.ToString("yyyyMMdd"));
                    IrfTable1.SetValue("ACT_END_DATE", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.ACT_END_DATE.ToString("yyyyMMdd"));
                    IrfTable1.SetValue("START_TM", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.START_TM.ToString("HH:mm:ss"));
                    IrfTable1.SetValue("END_TIME", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.END_TIME.ToString("HH:mm:ss"));
                    IrfTable1.SetValue("PREPARED_BY", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.PREPARED_BY);
                    IrfTable1.SetValue("REMARKS", request.ZDC_ROUTING_SUB_RFC_LS_GET_DATA.REMARKS);

                    IRfcStructure IrfTable2 = myfun.GetStructure("LS_OT_DETAILS");


                    IrfTable2.SetValue("BOX_TOTAL", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.BOX_TOTAL);
                    IrfTable2.SetValue("TTL_STAGING_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.TTL_STAGING_QTY);
                    IrfTable2.SetValue("ACT_VAL", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ACT_VAL);
                    IrfTable2.SetValue("STAG_SEC", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.STAG_SEC);
                    IrfTable2.SetValue("BIN_COUNT", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.BIN_COUNT);
                    IrfTable2.SetValue("ZRFS_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ZRFS_QTY);
                    IrfTable2.SetValue("PLTNO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PLTNO);
                    IrfTable2.SetValue("CRT_NO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.CRT_NO);
                    IrfTable2.SetValue("QC_DONE_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.QC_DONE_QTY);
                    IrfTable2.SetValue("QC_FAILED_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.QC_FAILED_QTY);
                    IrfTable2.SetValue("LOT_STATUS", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.LOT_STATUS);
                    IrfTable2.SetValue("PHY_SAP_PO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PHY_SAP_PO);
                    IrfTable2.SetValue("PROCESS_TODO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PROCESS_TODO);
                    IrfTable2.SetValue("COLOUR", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.COLOUR);
                    IrfTable2.SetValue("BARCODE_STAT", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.BARCODE_STAT);
                    IrfTable2.SetValue("ZSIZE", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ZSIZE);
                    IrfTable2.SetValue("FABRIC_DTL", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.FABRIC_DTL);
                    IrfTable2.SetValue("QC_BY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.QC_BY);
                    IrfTable2.SetValue("LOT_ATR", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.LOT_ATR);
                    IrfTable2.SetValue("APPROVED_BY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.APPROVED_BY);
                    IrfTable2.SetValue("PROC_ST_NO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PROC_ST_NO);
                    IrfTable2.SetValue("IS_LOCK_NO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.IS_LOCK_NO);
                    IrfTable2.SetValue("IS_LOG_DATE", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.IS_LOG_DATE);
                    IrfTable2.SetValue("IS_LOG_TM", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.IS_LOG_TM);
                    IrfTable2.SetValue("IS_DONE_DT", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.IS_DONE_DT);
                    IrfTable2.SetValue("IS_DONE_TM", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.IS_DONE_TM);
                    IrfTable2.SetValue("BARCD_STAT", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.BARCD_STAT);
                    IrfTable2.SetValue("PACK_SIZE_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PACK_SIZE_QTY);
                    IrfTable2.SetValue("SCANNER_NAME", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.SCANNER_NAME);
                    IrfTable2.SetValue("AC_QTY_OLD", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.AC_QTY_OLD);
                    IrfTable2.SetValue("AC_QTY_NEW", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.AC_QTY_NEW);
                    IrfTable2.SetValue("TT_TD_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.TT_TD_QTY);
                    IrfTable2.SetValue("ACT_REC_CRATE", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ACT_REC_CRATE);
                    IrfTable2.SetValue("DONE_CRATE", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.DONE_CRATE);
                    IrfTable2.SetValue("BGT_SAM", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.BGT_SAM);
                    IrfTable2.SetValue("ACT_SAM", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ACT_SAM);
                    IrfTable2.SetValue("WITH_PACK_SIZE", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.WITH_PACK_SIZE);
                    IrfTable2.SetValue("GRC_NO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.GRC_NO);
                    IrfTable2.SetValue("GRC_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.GRC_QTY);
                    IrfTable2.SetValue("GRC_VAL", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.GRC_VAL);
                    IrfTable2.SetValue("REMARKSS", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.REMARKSS);
                    IrfTable2.SetValue("BIN_NO", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.BIN_NO);
                    IrfTable2.SetValue("LOT_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.LOT_QTY);
                    IrfTable2.SetValue("SAMPLE_REC", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.SAMPLE_REC);
                    IrfTable2.SetValue("ACT_REC_DT", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ACT_REC_DT);
                    IrfTable2.SetValue("ACT_REC_TIME", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ACT_REC_TIME);
                    IrfTable2.SetValue("ACT_REC_HU", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ACT_REC_HU);
                    IrfTable2.SetValue("DONE_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.DONE_QTY);
                    IrfTable2.SetValue("PACK_SIZE_BGT_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PACK_SIZE_BGT_QTY);
                    IrfTable2.SetValue("PACK_SIZE_ACT_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PACK_SIZE_ACT_QTY);
                    IrfTable2.SetValue("MP_USED", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.MP_USED);
                    IrfTable2.SetValue("DONE_CARET", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.DONE_CARET);
                    IrfTable2.SetValue("BAL_QTY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.BAL_QTY);
                    IrfTable2.SetValue("QTY_VAL", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.QTY_VAL);
                    IrfTable2.SetValue("QC_DONE", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.QC_DONE);
                    IrfTable2.SetValue("PROCESS_STAT", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PROCESS_STAT);
                    IrfTable2.SetValue("ACT_REC_Q", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.ACT_REC_Q);
                    IrfTable2.SetValue("PROC_TAT", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.PROC_TAT);
                    IrfTable2.SetValue("EFFECIENCY", request.ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS.EFFECIENCY);


                    IRfcTable IrfTable = myfun.GetTable("LT_PROD");
                    foreach (var k in request.ZDC_ROUTING_SUB_RFC_LT_PROD)
                    {
                        IrfTable.Append();
                        //IrfTable.SetValue("GE_NO", k.GE_NO);
                        //IrfTable.SetValue("DCNO", k.DCNO);
                        //IrfTable.SetValue("TEXT", k.TEXT);
                        IrfTable.SetValue("EMP_CD", k.EMP_CD);
                        //IrfTable.SetValue("PO_NUM", k.PO_NUM);
                        IrfTable.SetValue("MAN_PO", k.MAN_PO);
                        IrfTable.SetValue("LOT_QTY", k.LOT_QTY);
                        IrfTable.SetValue("ASS_QTY", k.ASS_QTY);
                        IrfTable.SetValue("ACT_DONE", k.ACT_DONE);
                        IrfTable.SetValue("ACT_ST_DT", k.ACT_ST_DT);
                        IrfTable.SetValue("ACT_ST_TM", k.ACT_ST_TM);
                        IrfTable.SetValue("ACT_END_DT", k.ACT_END_DT);
                        IrfTable.SetValue("ACT_END_TM", k.ACT_END_TM);
                        IrfTable.SetValue("REMARKS", k.REMARKS);
                    }
                    IRfcStructure IrfTable3 = myfun.GetStructure("LS_COMP");
                    IrfTable3.SetValue("EBELN", request.ZDC_ROUTING_SUB_RFC_LS_COMP.EBELN);
                    IrfTable3.SetValue("VND_CONFM", request.ZDC_ROUTING_SUB_RFC_LS_COMP.VND_CONFM);
                    IrfTable3.SetValue("CLR_SIZE", request.ZDC_ROUTING_SUB_RFC_LS_COMP.CLR_SIZE);
                    IrfTable3.SetValue("PACK_NORMS", request.ZDC_ROUTING_SUB_RFC_LS_COMP.PACK_NORMS);
                    IrfTable3.SetValue("PACK_SIZE", request.ZDC_ROUTING_SUB_RFC_LS_COMP.PACK_SIZE);
                    IrfTable3.SetValue("TAFETA_BARCODE", request.ZDC_ROUTING_SUB_RFC_LS_COMP.TAFETA_BARCODE);
                    IrfTable3.SetValue("BARCODE_VEND", request.ZDC_ROUTING_SUB_RFC_LS_COMP.BARCODE_VEND);
                    IrfTable3.SetValue("APPR_QUALITY", request.ZDC_ROUTING_SUB_RFC_LS_COMP.APPR_QUALITY);
                    IrfTable3.SetValue("EACH_DESIGN", request.ZDC_ROUTING_SUB_RFC_LS_COMP.EACH_DESIGN);
                    IrfTable3.SetValue("PRIVATE_LABEL", request.ZDC_ROUTING_SUB_RFC_LS_COMP.PRIVATE_LABEL);
                    IrfTable3.SetValue("PIC_PR_LABEL", request.ZDC_ROUTING_SUB_RFC_LS_COMP.PIC_PR_LABEL);
                    IrfTable3.SetValue("CARTON", request.ZDC_ROUTING_SUB_RFC_LS_COMP.CARTON);
                    IrfTable3.SetValue("BUYING", request.ZDC_ROUTING_SUB_RFC_LS_COMP.BUYING);
                    IrfTable3.SetValue("SAMPLES", request.ZDC_ROUTING_SUB_RFC_LS_COMP.SAMPLES);
                    IrfTable3.SetValue("PPT_TYPE", request.ZDC_ROUTING_SUB_RFC_LS_COMP.PPT_TYPE);

                    myfun.Invoke(dest);


                    //IRfcTable IrfTable = myfun.GetTable("LT_DATA");
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
            //});

        }
    }
    public class ZDC_ROUTING_SUB_RFC
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<ZDC_ROUTING_SUB_RFC_response> Data;
        public ZDC_ROUTING_SUB_RFC()
        {
            Data = new List<ZDC_ROUTING_SUB_RFC_response>();

        }
    }
    public class ZDC_ROUTING_SUB_RFC_response
    {
        public string GE_NO { get; set; }
        public string DCNO { get; set; } = string.Empty;
        public string TEXT { get; set; }
        public string EMP_CD { get; set; } = string.Empty;
        public string PO_NUM { get; set; }


    }
    public class ZDC_ROUTING_SUB_RFC_request
    {
        public string IM_GATE_ENTRY { get; set; }
        public string IM_EBELN { get; set; }
        public string IM_ROUTING_NO { get; set; } = string.Empty;
        public string IM_PO { get; set; }
        public string IM_GRC { get; set; } = string.Empty;
        public string IM_ASN { get; set; } = string.Empty;
        public ZDC_ROUTING_SUB_RFC_LS_GET_DATA ZDC_ROUTING_SUB_RFC_LS_GET_DATA { get; set; }
        public ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS { get; set; }
        public List<ZDC_ROUTING_SUB_RFC_LT_PROD> ZDC_ROUTING_SUB_RFC_LT_PROD { get; set; }
        public ZDC_ROUTING_SUB_RFC_LS_COMP ZDC_ROUTING_SUB_RFC_LS_COMP { get; set; }

    }
    public class ZDC_ROUTING_SUB_RFC_LS_GET_DATA
    {
        public DateTime BGT_START_DATE { get; set; }
        public DateTime BGT_END_DATE { get; set; }
        public DateTime ACT_START_DATE { get; set; }
        public DateTime ACT_END_DATE { get; set; }
        public DateTime START_TM { get; set; }
        public DateTime END_TIME { get; set; }
        public string PREPARED_BY { get; set; }
        public string REMARKS { get; set; }

    }

    public class ZDC_ROUTING_SUB_RFC_LS_COMP
    {

        public string EBELN { get; set; }
        public string VND_CONFM { get; set; }
        public string CLR_SIZE { get; set; }
        public string PACK_NORMS { get; set; }
        public string PACK_SIZE { get; set; }
        public string TAFETA_BARCODE { get; set; }
        public string BARCODE_VEND { get; set; }
        public string APPR_QUALITY { get; set; }
        public string EACH_DESIGN { get; set; }
        public string PRIVATE_LABEL { get; set; }
        public string PIC_PR_LABEL { get; set; }
        public string CARTON { get; set; }
        public string BUYING { get; set; }
        public string SAMPLES { get; set; }
        public string PPT_TYPE { get; set; }



    }
    public class ZDC_ROUTING_SUB_RFC_LT_PROD
    {




        public string EMP_CD { get; set; }

        public string MAN_PO { get; set; }
        public string LOT_QTY { get; set; }
        public string ASS_QTY { get; set; }
        public string ACT_DONE { get; set; }
        public string ACT_ST_DT { get; set; }
        public string ACT_ST_TM { get; set; }
        public string ACT_END_DT { get; set; }
        public string ACT_END_TM { get; set; }
        public string REMARKS { get; set; }



    }
    public class ZDC_ROUTING_SUB_RFC_LS_OT_DETAILS
    {
        //public string BOX_TOTAL { get; set; }
        //public string TTL_STAGING_QTY { get; set; }
        //public string ACT_VAL { get; set; }
        //public string STAG_SEC { get; set; }
        //public string BIN_COUNT { get; set; }
        //public string ZRFS_QTY { get; set; }
        //public string PLTNO { get; set; }
        //public string CRT_NO { get; set; }
        //public string QC_DONE_QTY { get; set; }
        //public string QC_FAILED_QTY { get; set; }
        //public string LOT_STATUS { get; set; }
        //public string PHY_SAP_PO { get; set; }
        //public string PROCESS_TODO { get; set; }
        //public string COLOUR { get; set; }
        //public string BARCODE_STAT { get; set; }
        //public string ZSIZE { get; set; }
        //public string FABRIC_DTL { get; set; }
        //public string QC_BY { get; set; }
        //public string LOT_ATR { get; set; }
        //public string APPROVED_BY { get; set; }
        //public string PROC_ST_NO { get; set; }
        //public string IS_LOCK_NO { get; set; }
        //public string IS_LOG_DATE { get; set; }
        //public string IS_LOG_TM { get; set; }
        //public string IS_DONE_DT { get; set; }
        //public string IS_DONE_TM { get; set; }
        //public string BARCD_STAT { get; set; }
        //public string PACK_SIZE_QTY { get; set; }
        //public string SCANNER_NAME { get; set; }
        //public string AC_QTY_OLD { get; set; }
        //public string AC_QTY_NEW { get; set; }
        //public string TT_TD_QTY { get; set; }
        //public string ACT_REC_CRATE { get; set; }
        //public string DONE_CRATE { get; set; }
        //public string BGT_SAM { get; set; }
        //public string ACT_SAM { get; set; }
        //public string WITH_PACK_SIZE { get; set; }
        //public string GRC_NO { get; set; }
        //public string GRC_QTY { get; set; }
        //public string GRC_VAL { get; set; }
        //public string REMARKSS { get; set; }
        //public string BGT_ST_DT { get; set; }
        //public string BGT_END_DT { get; set; }
        //public string BGT_ST_TM { get; set; }
        //public string BGT_END_TM { get; set; }
        public string BOX_TOTAL { get; set; }
        public string TTL_STAGING_QTY { get; set; }
        public string ACT_VAL { get; set; }
        public string STAG_SEC { get; set; }
        public string BIN_COUNT { get; set; }
        public string ZRFS_QTY { get; set; }
        public string PLTNO { get; set; }
        public string CRT_NO { get; set; }
        public string QC_DONE_QTY { get; set; }
        public string QC_FAILED_QTY { get; set; }
        public string LOT_STATUS { get; set; }
        public string PHY_SAP_PO { get; set; }
        public string PROCESS_TODO { get; set; }
        public string COLOUR { get; set; }
        public string BARCODE_STAT { get; set; }
        public string ZSIZE { get; set; }
        public string FABRIC_DTL { get; set; }
        public string QC_BY { get; set; }
        public string LOT_ATR { get; set; }
        public string APPROVED_BY { get; set; }
        public string PROC_ST_NO { get; set; }
        public string IS_LOCK_NO { get; set; }
        public string IS_LOG_DATE { get; set; }
        public string IS_LOG_TM { get; set; }
        public string IS_DONE_DT { get; set; }
        public string IS_DONE_TM { get; set; }
        public string BARCD_STAT { get; set; }
        public string PACK_SIZE_QTY { get; set; }
        public string SCANNER_NAME { get; set; }
        public string AC_QTY_OLD { get; set; }
        public string AC_QTY_NEW { get; set; }
        public string TT_TD_QTY { get; set; }
        public string ACT_REC_CRATE { get; set; }
        public string DONE_CRATE { get; set; }
        public string BGT_SAM { get; set; }
        public string ACT_SAM { get; set; }
        public string WITH_PACK_SIZE { get; set; }
        public string GRC_NO { get; set; }
        public string GRC_QTY { get; set; }
        public string GRC_VAL { get; set; }
        public string REMARKSS { get; set; }
        public string BIN_NO { get; set; }
        public string LOT_QTY { get; set; }
        public string SAMPLE_REC { get; set; }
        public string ACT_REC_DT { get; set; }
        public string ACT_REC_TIME { get; set; }
        public string ACT_REC_HU { get; set; }
        public string DONE_QTY { get; set; }
        public string PACK_SIZE_BGT_QTY { get; set; }
        public string PACK_SIZE_ACT_QTY { get; set; }
        public string MP_USED { get; set; }
        public string DONE_CARET { get; set; }
        public string BAL_QTY { get; set; }
        public string QTY_VAL { get; set; }
        public string QC_DONE { get; set; }
        public string PROCESS_STAT { get; set; }
        public string ACT_REC_Q { get; set; }
        public string PROC_TAT { get; set; }
        public string EFFECIENCY { get; set; }



    }

}