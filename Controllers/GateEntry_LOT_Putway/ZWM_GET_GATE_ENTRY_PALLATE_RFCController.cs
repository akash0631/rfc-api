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
    public class ZWM_GET_GATE_ENTRY_PALLATE_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZWM_GET_GATE_ENTRY_PALLATE_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if ( request.IM_WERKS != null)
                    {

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZWM_GET_GATE_ENTRY_PALLATE_RFC");

                        myfun.SetValue("IM_USER", request.IM_USER);
                        myfun.SetValue("IM_WERKS", request.IM_WERKS);
                        myfun.SetValue("IM_GATE", request.IM_GATE);
                        myfun.SetValue("IM_PALL", request.IM_PALL);

                        myfun.Invoke(dest);
                        //IRfcTable IrfTable = myfun.GetTable("IT_DATA");
                        //IRfcTable IrfTable1 = myfun.GetTable("ET_EAN_DATA");

                        IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");

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
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = true,
                                Message = "" + SAP_Message + ""

                            });
                        }
                        //int i = 0;
                        //    var etdata = IrfTable.AsEnumerable().Select(row => new
                        //    {
                        //        DOCNO = row.GetString("DOCNO").ToString()
                              
                        //    }).ToList();
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
                        //    return Request.CreateResponse(HttpStatusCode.OK, new
                        //    {
                        //        Status = true,
                        //        Message = "" + SAP_Message + "",
                        //        Data = new
                        //        {
                        //            ET_Data = etdata

                        //        }
                        //        //Data = new { 
                        //        //    ET_Data = etdata,
                        //        //    ET_EAN_DATA = eteandata
                        //        //}

                        //    });
                        //}
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
    public class ZWM_GET_GATE_ENTRY_PALLATE_RFCRequest
    {
        public string IM_USER { get; set; }
        public string IM_WERKS { get; set; }
        public string IM_GATE { get; set; }
        public string IM_PALL { get; set; }

    }
}