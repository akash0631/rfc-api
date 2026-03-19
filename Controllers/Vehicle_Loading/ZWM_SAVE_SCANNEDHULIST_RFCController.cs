using DocumentFormat.OpenXml.Spreadsheet;
using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Razor.Parser.SyntaxTree;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    public class ZWM_SAVE_SCANNEDHULIST_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZWM_SAVE_SCANNEDHULIST_RFCRequest request)
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
                        myfun = rfcrep.CreateFunction("ZWM_SAVE_SCANNEDHULIST_RFC");

                        myfun.SetValue("IM_USER", request.IM_USER);
                        myfun.SetValue("IM_PLANT", request.IM_PLANT);
                        myfun.SetValue("IM_VEHICLE", request.IM_VEHICLE);
                        //myfun.SetValue("HU_LIST", request.HU_LIST);
                        myfun.SetValue("IM_REMOVE", request.IM_REMOVE);

                        IRfcTable IrfTable1 = myfun.GetTable("HU_LIST");
                        foreach (var k in request.HU_LIST)
                        {
                            IrfTable1.Append();

                            IrfTable1.SetValue("SRC_STORE", k.SRC_STORE);
                            IrfTable1.SetValue("DST_STORE", k.DST_STORE);
                            IrfTable1.SetValue("LRNO", k.LRNO);
                            IrfTable1.SetValue("EXTERNAL_HU", k.EXTERNAL_HU);
                            IrfTable1.SetValue("INTERNAL_HU", k.INTERNAL_HU);
                            IrfTable1.SetValue("QUANTITY", k.QUANTITY);
                            IrfTable1.SetValue("PALETTE", k.PALETTE);
                            IrfTable1.SetValue("CLA_BIN", k.CLA_BIN);
                            IrfTable1.SetValue("DCLA_STATUS", k.DCLA_STATUS);
                            IrfTable1.SetValue("UOM", k.UOM);
                            IrfTable1.SetValue("SCAN", k.SCAN);
                            
                           

                        }



                        myfun.Invoke(dest);
                        //IRfcTable IrfTable = myfun.GetTable("ET_HULIST");
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
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = true,
                                Message = "" + SAP_Message + ""

                            });
                        }
                        //    var etdata = IrfTable.AsEnumerable().Select(row => new
                        //        {
                        //            SRC_STORE = row.GetString("SRC_STORE").ToString(),
                        //            DST_STORE = row.GetString("DST_STORE").ToString(),
                        //            LRNO = row.GetString("LRNO").ToString(),
                        //            EXTERNAL_HU = row.GetString("EXTERNAL_HU").ToString(),
                        //            QUANTITY = row.GetString("QUANTITY").ToString(),
                        //            PALETTE = row.GetString("PALETTE").ToString(),
                        //            CLA_BIN = row.GetString("CLA_BIN").ToString(),
                        //            DCLA_STATUS = row.GetString("DCLA_STATUS").ToString(),
                        //            UOM = row.GetString("UOM").ToString()

                        //        }).ToList();

                        //        return Request.CreateResponse(HttpStatusCode.OK, new
                        //        {
                        //            Status = true,
                        //            Message = "" + SAP_Message + "",
                        //            Data = new
                        //            {
                        //                ET_Data = etdata

                        //            }
                        //            //        //Data = new { 
                        //            //        //    ET_Data = etdata,
                        //            //        //    ET_EAN_DATA = eteandata
                        //            //        //}

                        //               });

                        //        }
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
    public class ZWM_SAVE_SCANNEDHULIST_RFCRequest
    {
        //public string HU_LIST { get; set; }
        public string IM_USER { get; set; }
        public string IM_PLANT { get; set; }
        public string IM_VEHICLE { get; set; }
        public string IM_REMOVE { get; set; }
        public List<HU_LISTRequest> HU_LIST { get; set; }


    }
    public class HU_LISTRequest
    {
        public string SRC_STORE { get; set; }
        public string DST_STORE { get; set; }
        public string LRNO { get; set; }
        public string EXTERNAL_HU { get; set; }
        public string INTERNAL_HU { get; set; }
        public string QUANTITY { get; set; }
        public string PALETTE { get; set; }
        public string CLA_BIN { get; set; }
        public string DCLA_STATUS { get; set; }
        public string UOM { get; set; }
        public string SCAN { get; set; }

    }
}