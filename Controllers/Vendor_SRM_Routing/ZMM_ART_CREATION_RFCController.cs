using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Controllers.VendorSRM
{
    public class ZMM_ART_CREATION_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZMM_ART_CREATION_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if ( request.VENDOR != null)
                    {

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZMM_ART_CREATION_RFC");
                        IRfcTable IrfTable = myfun.GetTable("IM_DATA");
                        //IM_DATA
                        IrfTable.Append();
                        IrfTable.SetValue("HSN_CODE", request.HSN_CODE);
                        IrfTable.SetValue("SUB_DIV", request.SUB_DIV);
                        IrfTable.SetValue("MC_CD", request.MC_CD);
                        IrfTable.SetValue("VENDOR", request.VENDOR);
                        IrfTable.SetValue("DSG_NO", request.DSG_NO);
                        IrfTable.SetValue("MRP", request.MRP);
                        IrfTable.SetValue("SEASON", request.SEASON);
                        IrfTable.SetValue("ARTICLE_DES1", request.ARTICLE_DES1);
                        IrfTable.SetValue("PRICE_BAND_CATEGORY", request.PRICE_BAND_CATEGORY);
                        IrfTable.SetValue("M_MAIN_MVGR", request.M_MAIN_MVGR);
                        IrfTable.SetValue("M_MACRO_MVGR", request.M_MACRO_MVGR);
                        IrfTable.SetValue("M_FAB_DIV", request.M_FAB_DIV);
                        IrfTable.SetValue("M_FAB", request.M_FAB);
                        IrfTable.SetValue("M_FAB2", request.M_FAB2);
                        IrfTable.SetValue("M_YARN", request.M_YARN);
                        IrfTable.SetValue("M_YARN02", request.M_YARN_02);
                        IrfTable.SetValue("M_WEAVE_2", request.M_WEAVE_2);
                        IrfTable.SetValue("M_COMPOSITION", request.M_COMPOSITION);
                        IrfTable.SetValue("M_FINISH", request.M_FINISH);
                        IrfTable.SetValue("M_CONSTRUCTION", request.M_CONSTRUCTION);
                        IrfTable.SetValue("M_SHADE", request.M_SHADE);
                        IrfTable.SetValue("M_LYCRA", request.M_LYCRA);
                        IrfTable.SetValue("M_GSM", request.M_GSM);
                        IrfTable.SetValue("M_COUNT", request.M_COUNT);
                        IrfTable.SetValue("M_OUNZ", request.M_OUNZ);
                        IrfTable.SetValue("M_COLLAR", request.M_COLLAR);
                        IrfTable.SetValue("M_NECK_BAND_STYLE", request.M_NECK_BAND_STYLE);
                        IrfTable.SetValue("M_PLACKET", request.M_PLACKET);
                        IrfTable.SetValue("M_BLT_MAIN_STYLE", request.M_BLT_MAIN_STYLE);
                        IrfTable.SetValue("M_SUB_STYLE_BLT", request.M_SUB_STYLE_BLT);
                        IrfTable.SetValue("M_SLEEVES_MAIN_STYLE", request.M_SLEEVES_MAIN_STYLE);
                        IrfTable.SetValue("M_BTM_FOLD", request.M_BTM_FOLD);
                        IrfTable.SetValue("M_NECK_BAND", request.M_NECK_BAND);
                        IrfTable.SetValue("M_FO_BTN_STYLE", request.M_FO_BTN_STYLE);
                        IrfTable.SetValue("NO_OF_POCKET", request.NO_OF_POCKET);
                        IrfTable.SetValue("M_POCKET", request.M_POCKET);
                        IrfTable.SetValue("POCKET_PLACEMENT", request.POCKET_PLACEMENT);
                        IrfTable.SetValue("M_FIT", request.M_FIT);
                        IrfTable.SetValue("M_PATTERN", request.M_PATTERN);
                        IrfTable.SetValue("M_LENGTH", request.M_LENGTH);
                        IrfTable.SetValue("M_DC_SUB_STYLE", request.M_DC_SUB_STYLE);
                        IrfTable.SetValue("M_BTN_MAIN_MVGR", request.M_BTN_MAIN_MVGR);
                        IrfTable.SetValue("M_ZIP", request.M_ZIP);
                        IrfTable.SetValue("M_ZIP_COL", request.M_ZIP_COL);
                        IrfTable.SetValue("M_PRINT_TYPE", request.M_PRINT_TYPE);
                        IrfTable.SetValue("M_PRINT_PLACEMENT", request.M_PRINT_PLACEMENT);
                        IrfTable.SetValue("M_PRINT_STYLE", request.M_PRINT_STYLE);
                        IrfTable.SetValue("M_PATCHES", request.M_PATCHES);
                        IrfTable.SetValue("M_PATCH_TYPE", request.M_PATCH_TYPE);
                        IrfTable.SetValue("M_EMBROIDERY", request.M_EMBROIDERY);
                        IrfTable.SetValue("M_EMB_TYPE", request.M_EMB_TYPE);
                        IrfTable.SetValue("M_WASH", request.M_WASH);
                        IrfTable.SetValue("M_PD", request.M_PD);
                        IrfTable.SetValue("MVGR_BRAND_VENDOR", request.MVGR_BRAND_VENDOR);




                        myfun.Invoke(dest);
                        //IRfcTable IrfTable = myfun.GetTable("LT_DATA");
                        //IRfcTable IrfTable1 = myfun.GetTable("ET_EAN_DATA");

                        IRfcTable E_RETURN = myfun.GetTable("EX_DATA");

                        string SAP_TYPE = E_RETURN.GetValue("MSG_TYP").ToString();
                        string SAP_ART = E_RETURN.GetValue("SAP_ART").ToString();
                        string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                        if (SAP_TYPE == "E")
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = false,
                                Message = "" + SAP_Message + ""

                            });
                        }
                        else
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = true,
                                Message = "" + SAP_Message + "",
                                SAP_ART = SAP_ART

                            });
                        
                        }
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = true,
                            Message = "Request Not Valid"

                        });
                    }
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status = false,
                        Message = ex.Message
                    });
                }
            });
        }
    }
    public class ZMM_ART_CREATION_RFCRequest
    {
        public string HSN_CODE { get; set; }
        public string SUB_DIV { get; set; }
        public string MC_CD { get; set; }
        public string VENDOR { get; set; }
        public string DSG_NO { get; set; }
        public string MRP { get; set; }
        public string SEASON { get; set; }
        public string ARTICLE_DES1 { get; set; }
        public string PRICE_BAND_CATEGORY { get; set; }
        public string M_MAIN_MVGR { get; set; }
        public string M_MACRO_MVGR { get; set; }
        public string M_FAB_DIV { get; set; }
        public string M_FAB { get; set; }
        public string M_FAB2 { get; set; }
        public string M_YARN { get; set; }
        public string M_YARN_02 { get; set; }
        public string M_WEAVE_2 { get; set; }
        public string M_COMPOSITION { get; set; }
        public string M_FINISH { get; set; }
        public string M_CONSTRUCTION { get; set; }
        public string M_SHADE { get; set; }
        public string M_LYCRA { get; set; }
        public string M_GSM { get; set; }
        public string M_COUNT { get; set; }
        public string M_OUNZ { get; set; }
        public string M_COLLAR { get; set; }
        public string M_NECK_BAND_STYLE { get; set; }
        public string M_PLACKET { get; set; }
        public string M_BLT_MAIN_STYLE { get; set; }
        public string M_SUB_STYLE_BLT { get; set; }
        public string M_SLEEVES_MAIN_STYLE { get; set; }
        public string M_BTM_FOLD { get; set; }
        public string M_NECK_BAND { get; set; }
        public string M_FO_BTN_STYLE { get; set; }
        public string NO_OF_POCKET { get; set; }
        public string M_POCKET { get; set; }
        public string POCKET_PLACEMENT { get; set; }
        public string M_FIT { get; set; }
        public string M_PATTERN { get; set; }
        public string M_LENGTH { get; set; }
        public string M_DC_SUB_STYLE { get; set; }
        public string M_BTN_MAIN_MVGR { get; set; }
        public string M_ZIP { get; set; }
        public string M_ZIP_COL { get; set; }
        public string M_PRINT_TYPE { get; set; }
        public string M_PRINT_PLACEMENT { get; set; }
        public string M_PRINT_STYLE { get; set; }
        public string M_PATCHES { get; set; }
        public string M_PATCH_TYPE { get; set; }
        public string M_EMBROIDERY { get; set; }
        public string M_EMB_TYPE { get; set; }
        public string M_WASH { get; set; }
        public string M_PD { get; set; }
        public string MVGR_BRAND_VENDOR { get; set; }


    }
}