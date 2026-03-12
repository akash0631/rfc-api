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

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    public class ZWM_HU_SELECTION_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZWM_HU_SELECTION_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if (request.IM_USER != null)
                    {

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZWM_HU_SELECTION_RFC");

                        myfun.SetValue("IM_USER", request.IM_USER);
                        myfun.SetValue("IM_PLANT", request.IM_PLANT);
                        myfun.SetValue("IM_VEH", request.IM_VEH);
                        myfun.SetValue("IM_TRANSPORT_CODE", request.IM_TRANSPORT_CODE);
                        myfun.SetValue("IM_SEAL_NO", request.IM_SEAL_NO);
                        myfun.SetValue("IM_DRIVER_NAME", request.IM_DRIVER_NAME);
                        myfun.SetValue("IM_DRIVER_MOB", request.IM_DRIVER_MOB);
                        myfun.SetValue("IM_HUB_FLAG", request.IM_HUB_FLAG);
                        myfun.SetValue("IM_STORE_FLAG", request.IM_STORE_FLAG);
                        myfun.SetValue("IM_HUB", request.IM_HUB);
                        myfun.SetValue("IM_GRP", request.IM_GRP);
                       
                        IRfcTable IrfTable1 = myfun.GetTable("STORE_LIST");
                        foreach (var k in request.STORE_LIST)
                        {
                            IrfTable1.Append();
                           
                            IrfTable1.SetValue("STORE", k.STORE);
                         
                        }





                        myfun.Invoke(dest);
                        IRfcTable IrfTable = myfun.GetTable("ET_HULIST");
                        //IRfcTable IrfTable1 = myfun.GetTable("ET_EAN_DATA");

                        IRfcStructure E_RETURN = myfun.GetStructure("ET_ERROR");

                        string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
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
                            //    return Request.CreateResponse(HttpStatusCode.OK, new
                            //    {
                            //        Status = true,
                            //        Message = "" + SAP_Message + ""

                            //    });
                            //}
                            //int i = 0;
                            var etdata = IrfTable.AsEnumerable().Select(row => new
                            {
                                SRC_STORE = row.GetString("SRC_STORE").ToString(),
                                DST_STORE = row.GetString("DST_STORE").ToString(),
                                LRNO = row.GetString("LRNO").ToString(),
                                EXTERNAL_HU = row.GetString("EXTERNAL_HU").ToString(),
                               INTERNAL_HU = row.GetString("INTERNAL_HU").ToString(),
                                QUANTITY = row.GetString("QUANTITY").ToString(),
                                PALETTE = row.GetString("PALETTE").ToString(),
                                CLA_BIN = row.GetString("CLA_BIN").ToString(),
                                DCLA_STATUS = row.GetString("DCLA_STATUS").ToString(),
                                UOM = row.GetString("UOM").ToString(),
                                SCAN = row.GetString("SCAN").ToString()

                            }).ToList();
                            //    //var eteandata = IrfTable1.AsEnumerable().Select(row => new GetPickListEtEanData
                            //    //    {
                            //    //        MANDT = row.GetString("MANDT"),
                            //    //        MATNR = row.GetString("MATNR"),
                            //    //        MEINH = row.GetString("MEINH"),
                            //    //        UMREZ = row.GetString("UMREZ"),
                            //    //        UMREN = row.GetString("UMREN"),
                            //    //        EANNR = row.GetString("EANNR"),
                            //    //        EAN11 = row.GetString("EAN11"),
                            //    //        NUMTP = row.GetString("NUMTP"),
                            //    //        LAENG = row.GetString("LAENG"),
                            //    //        BREIT = row.GetString("BREIT"),
                            //    //        HOEHE = row.GetString("HOEHE"),
                            //    //        MEABM = row.GetString("MEABM"),
                            //    //        VOLUM = row.GetString("VOLUM"),
                            //    //        VOLEH = row.GetString("VOLEH"),
                            //    //        BRGEW = row.GetString("BRGEW"),
                            //    //        GEWEI = row.GetString("GEWEI"),
                            //    //        MESUB = row.GetString("MESUB"),
                            //    //        ATINN = row.GetString("ATINN"),
                            //    //        MESRT = row.GetString("MESRT"),
                            //    //        XFHDW = row.GetString("XFHDW"),
                            //    //        XBEWW = row.GetString("XBEWW"),
                            //    //        KZWSO = row.GetString("KZWSO"),
                            //    //        MSEHI = row.GetString("MSEHI"),
                            //    //        BFLME_MARM = row.GetString("BFLME_MARM"),
                            //    //        GTIN_VARIANT = row.GetString("GTIN_VARIANT"),
                            //    //        NEST_FTR = row.GetString("NEST_FTR"),
                            //    //        MAX_STACK = row.GetString("MAX_STACK"),
                            //    //        TOP_LOAD_FULL = row.GetString("TOP_LOAD_FULL"),
                            //    //        TOP_LOAD_FULL_UOM = row.GetString("TOP_LOAD_FULL_UOM"),
                            //    //        CAPAUSE = row.GetString("CAPAUSE"),
                            //    //        TY2TQ = row.GetString("TY2TQ"),
                            //    //        DUMMY_UOM_INCL_EEW_PS = row.GetString("DUMMY_UOM_INCL_EEW_PS"),
                            //    //        CWM_TY2TQ = row.GetString("/CWM/TY2TQ"),
                            //    //        STTPEC_NCODE = row.GetString("/STTPEC/NCODE"),
                            //    //        STTPEC_NCODE_TY = row.GetString("/STTPEC/NCODE_TY"),
                            //    //        STTPEC_RCODE = row.GetString("/STTPEC/RCODE"),
                            //    //        STTPEC_SERUSE = row.GetString("/STTPEC/SERUSE"),
                            //    //        STTPEC_SYNCCHG = row.GetString("/STTPEC/SYNCCHG"),
                            //    //        STTPEC_SERNO_MANAGED = row.GetString("/STTPEC/SERNO_MANAGED"),
                            //    //        STTPEC_SERNO_PROV_BUP = row.GetString("/STTPEC/SERNO_PROV_BUP"),
                            //    //        STTPEC_UOM_SYNC = row.GetString("/STTPEC/UOM_SYNC"),
                            //    //        STTPEC_SER_GTIN = row.GetString("/STTPEC/SER_GTIN"),
                            //    //        PCBUT = row.GetString("PCBUT"),
                            //    //    }).ToList();

                            //    //var eteandata
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = true,
                                Message = "" + SAP_Message + "",
                                Data = new
                                {
                                    ET_Data = etdata

                                }
                                //        //Data = new { 
                                //        //    ET_Data = etdata,
                                //        //    ET_EAN_DATA = eteandata
                                //        //}

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
    public class STORE_LISTRequest
    {
        public string STORE { get; set; }
    }
        public class ZWM_HU_SELECTION_RFCRequest
    {
        public string IM_USER { get; set; }
        public string IM_PLANT { get; set; }
        public string IM_VEH { get; set; }
        public string IM_TRANSPORT_CODE { get; set; }
        public string IM_SEAL_NO { get; set; }
        public string IM_DRIVER_NAME { get; set; }
        public string IM_DRIVER_MOB { get; set; }
        public string IM_HUB_FLAG { get; set; }
        public string IM_STORE_FLAG { get; set; }
        public string IM_HUB { get; set; }
        public string IM_GRP { get; set; }
        public List<STORE_LISTRequest> STORE_LIST { get;  set; }
    }
}