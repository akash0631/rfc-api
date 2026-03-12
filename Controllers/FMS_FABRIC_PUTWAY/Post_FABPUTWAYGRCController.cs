using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using b = System.Web.Mvc;
using FMS_Fabric_Putway_Api.Models;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Policy;
using System.Xml.Linq;
using Vendor_Application_MVC.Controllers;
using Vendor_Application_MVC.Models;

namespace FMS_Fabric_Putway_Api.Controllers.API
{
    public class Post_FABPUTWAYGRCController : BaseController
    {
        // GET: HUNameCheck
        [b.HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] Post_FABPUTWAYGRCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if (request.IM_USER != "" && request.IM_USER != null &&
                        request.IM_GRC != "" && request.IM_GRC != null
                        )
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZFMS_RFC_FABPUTWAYGRC_POST"); //RfcFunctionName
                                                                                     //IRfcStructure E_Data = myfun.GetStructure("ET_DATA");
                        myfun.SetValue("IM_USER", request.IM_USER);
                        myfun.SetValue("IM_GRC", request.IM_GRC);
                        IRfcTable poItems = myfun.GetTable("IT_DATA");

                        for (int i = 0; i < request.data.Count; ++i)
                        {
                            IRfcStructure IrfTable = poItems.Metadata.LineType.CreateStructure();
                            IrfTable.SetValue("SITE", request.data[i].SITE);
                            IrfTable.SetValue("BIN", request.data[i].BIN);
                            IrfTable.SetValue("MATERIAL", request.data[i].MATERIAL);
                            IrfTable.SetValue("SCAN_QTY", request.data[i].SCAN_QTY);
                            IrfTable.SetValue("BATCH", request.data[i].BATCH);
                            IrfTable.SetValue("GRC_NO", request.data[i].GRC_NO);
                            IrfTable.SetValue("GR_LINE", request.data[i].GR_LINE);

                            poItems.Append(IrfTable);
                        }
                        // Set the table back to the function
                        //myfun.SetValue("ZWM_BIN_SCAN_T", poItems);
                        //IRfcTable IrfTable = myfun.GetTable("IT_DATA");
                        //var k = IrfTable.GetTable("ZWM_BIN_SCAN_T");
                        //IrfTable.SetValue("SITE", request.SITE);
                        //IrfTable.SetValue("BIN", request.BIN);
                        //IrfTable.SetValue("MATERIAL", request.MATERIAL);
                        //IrfTable.SetValue("SCAN_QTY", request.SCAN_QTY);
                        //IrfTable.SetValue("BATCH", request.BATCH);
                        //IrfTable.SetValue("GRC_NO", request.GRC_NO);
                        //IrfTable.SetValue("GR_LINE", request.GR_LINE);


                        myfun.Invoke(dest);
                        //IRfcTable IrfTable = myfun.GetTable("IT_DATA");

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
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = true,
                            Message = "" + SAP_Message + ""

                        });
                        //else
                        //{

                        //    IRfcTable IrfTable = myfun.GetTable("ET_EAN_DATA");
                        //    Validate_GRCResponse validate_GRCResponse = new Validate_GRCResponse();
                        //    List<scan_barcodeResponse> scan_barcodeResponse1 = new List<scan_barcodeResponse>();
                        //    List <article_batch_wise_qty_matchResponse> article_batch_wise_qty_matchResponse1 = new List<article_batch_wise_qty_matchResponse>();
                        //    for (int i = 0; i < IrfTable.RowCount; ++i)
                        //    {
                        //        scan_barcodeResponse scan_barcodeResponse = new scan_barcodeResponse();
                        //        scan_barcodeResponse.material = IrfTable[i].GetString("MATNR");
                        //        scan_barcodeResponse.qty = IrfTable[i].GetString("UMREZ");
                        //        scan_barcodeResponse.barcode = IrfTable[i].GetString("EAN11");
                        //        scan_barcodeResponse1.Add(scan_barcodeResponse);
                        //    }
                        //    validate_GRCResponse.scan_barcode=scan_barcodeResponse1;
                        //    IRfcTable IrfTable1 = myfun.GetTable("ET_DATA");

                        //    for (int i = 0; i < IrfTable1.RowCount; ++i)
                        //    {
                        //        article_batch_wise_qty_matchResponse article_batch_wise_qty_matchResponse = new article_batch_wise_qty_matchResponse();
                        //        article_batch_wise_qty_matchResponse.WAREHOUSE = IrfTable1[i].GetString("WAREHOUSE");
                        //        article_batch_wise_qty_matchResponse.SITE = IrfTable1[i].GetString("SITE");
                        //        article_batch_wise_qty_matchResponse.SLOC = IrfTable1[i].GetString("SLOC");
                        //        article_batch_wise_qty_matchResponse.CRATE = IrfTable1[i].GetString("CRATE");
                        //        article_batch_wise_qty_matchResponse.BIN_TYPE = IrfTable1[i].GetString("BIN_TYPE");
                        //        article_batch_wise_qty_matchResponse.BIN = IrfTable1[i].GetString("BIN");
                        //        article_batch_wise_qty_matchResponse.MATERIAL = IrfTable1[i].GetString("MATERIAL");
                        //        article_batch_wise_qty_matchResponse.SCAN_QTY = IrfTable1[i].GetString("SCAN_QTY");
                        //        article_batch_wise_qty_matchResponse.KEY = IrfTable1[i].GetString("KEY");
                        //        article_batch_wise_qty_matchResponse.SOURCE_BIN = IrfTable1[i].GetString("SOURCE_BIN");
                        //        article_batch_wise_qty_matchResponse.BATCH = IrfTable1[i].GetString("BATCH");
                        //        article_batch_wise_qty_matchResponse.REQ_QTY = IrfTable1[i].GetString("REQ_QTY");
                        //        article_batch_wise_qty_matchResponse.UOM = IrfTable1[i].GetString("UOM");
                        //        article_batch_wise_qty_matchResponse.GRC_NO = IrfTable1[i].GetString("GRC_NO");
                        //        article_batch_wise_qty_matchResponse.MJAHR = IrfTable1[i].GetString("MJAHR");
                        //        article_batch_wise_qty_matchResponse.GR_LINE = IrfTable1[i].GetString("GR_LINE");
                        //        article_batch_wise_qty_matchResponse.PUR_NO = IrfTable1[i].GetString("PUR_NO");
                        //        article_batch_wise_qty_matchResponse.PUR_LINE = IrfTable1[i].GetString("PUR_LINE");
                        //        article_batch_wise_qty_matchResponse1.Add(article_batch_wise_qty_matchResponse);

                        //    }
                        //    validate_GRCResponse.article_batch_wise_qty_match= article_batch_wise_qty_matchResponse1;
                        //    Authenticate.Data = validate_GRCResponse;
                        //    Authenticate.Status = true;
                        //    Authenticate.Message = "" + SAP_Message + "";
                        //    return Request.CreateResponse(HttpStatusCode.OK, Authenticate);

                        //}


                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status = false,
                            Message = "All request field is Mandatory."

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
}