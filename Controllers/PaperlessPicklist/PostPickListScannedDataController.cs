using DocumentFormat.OpenXml.Math;
using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    public class PostPickListScannedDataController : BaseController
    {
        private const int TIMEOUT_SECONDS = 30;

        public async Task<HttpResponseMessage> Post([FromBody] PostPicklistDataRequest request)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TIMEOUT_SECONDS)))
            {
                try
                {
                    var task = Task.Run(() =>
                    {
                        Validate_GRC Authenticate = new Validate_GRC();

                        if (request.IT_DATA != null)
                        {
                            RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                            RfcRepository rfcrep = dest.Repository;

                            IRfcFunction myfun = rfcrep.CreateFunction("ZWM_RFC_PICKLIST_SCAN_POST");

                            myfun.SetValue("IM_USER", request.IM_USER);
                            myfun.SetValue("IM_WERKS", request.IM_WERKS);
                            myfun.SetValue("IM_PICNR", request.IM_PICNR);
                            IRfcTable rfcTable = myfun.GetTable("IT_DATA");

                            foreach (var data in request.IT_DATA)
                            {
                                rfcTable.Append();
                                rfcTable.SetValue("WM_NO", data.WM_NO);
                                rfcTable.SetValue("MATERIAL", data.Material);
                                rfcTable.SetValue("PLANT", data.Plant);
                                rfcTable.SetValue("STOR_LOC", data.Stor_Loc);
                                rfcTable.SetValue("BATCH", data.Batch);
                                rfcTable.SetValue("CRATE", data.Crate);
                                rfcTable.SetValue("BIN", data.Bin);
                                rfcTable.SetValue("STORAGE_TYPE", data.Storage_Type);
                                rfcTable.SetValue("MEINS", data.MEINS);
                                rfcTable.SetValue("AVL_STOCK", data.Avl_Stock);
                                rfcTable.SetValue("OPEN_STOCK", data.Open_Stock);
                                rfcTable.SetValue("SCAN_QTY", data.Scan_Qty);
                                rfcTable.SetValue("PICNR", data.PICNR);
                                rfcTable.SetValue("PICK_QTY", data.Pick_Qty);
                                rfcTable.SetValue("HU_NO", data.Hu_No);
                                rfcTable.SetValue("BARCODE", data.Barcode);
                                rfcTable.SetValue("MATKL", data.Matkl);
                                rfcTable.SetValue("WGBEZ", data.WGBEZ);
                                rfcTable.SetValue("SONUM", data.Sonum);
                                rfcTable.SetValue("DELNUM", data.Delnum);
                                rfcTable.SetValue("POSNR", data.Posnr);
                                rfcTable.SetValue("GNATURE", data.GNature);
                                rfcTable.SetValue("SAMMG", data.Sammg);
                                rfcTable.SetValue("PICK_STATUS", data.Pick_Status);
                            }

                            myfun.Invoke(dest);

                            IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
                            string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                            string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();

                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = SAP_TYPE != "E",
                                Message = SAP_Message
                            });
                        }
                        else
                        {
                            return Request.CreateResponse(HttpStatusCode.BadRequest, new
                            {
                                Status = false,
                                Message = "Request Not Valid"
                            });
                        }

                    }, cts.Token);

                    var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
                    if (completedTask == task)
                    {
                        return await task; // Completed within timeout
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.RequestTimeout, new
                        {
                            Status = false,
                            Message = "Operation timed out after 30 seconds."
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
            }
        }
    }

    //public class PostPickListScannedDataController: BaseController
    //{
    //    public async Task<HttpResponseMessage> Post([FromBody] PostPicklistDataRequest request)
    //    {
    //        return await Task.Run(() =>
    //        {
    //            try
    //            {
    //                Validate_GRC Authenticate = new Validate_GRC();
    //                if (request.IT_DATA != null)
    //                {

    //                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
    //                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
    //                    // Get RfcTable from SAP
    //                    RfcRepository rfcrep = dest.Repository;

    //                    IRfcFunction myfun = null;
    //                    myfun = rfcrep.CreateFunction("ZWM_RFC_PICKLIST_SCAN_POST");

    //                    myfun.SetValue("IM_USER", request.IM_USER);
    //                    myfun.SetValue("IM_WERKS", request.IM_WERKS);
    //                    myfun.SetValue("IM_PICNR", request.IM_PICNR);
    //                    IRfcTable rfcTable = myfun.GetTable("IT_DATA");

    //                    // Populate the table with the data
    //                    foreach (var data in request.IT_DATA)
    //                    {
    //                        rfcTable.Append();
    //                        rfcTable.SetValue("WM_NO", data.WM_NO);
    //                        rfcTable.SetValue("MATERIAL", data.Material);
    //                        rfcTable.SetValue("PLANT", data.Plant);
    //                        rfcTable.SetValue("STOR_LOC", data.Stor_Loc);
    //                        rfcTable.SetValue("BATCH", data.Batch);
    //                        rfcTable.SetValue("CRATE", data.Crate);
    //                        rfcTable.SetValue("BIN", data.Bin);
    //                        rfcTable.SetValue("STORAGE_TYPE", data.Storage_Type);
    //                        rfcTable.SetValue("MEINS", data.MEINS);
    //                        rfcTable.SetValue("AVL_STOCK", data.Avl_Stock);
    //                        rfcTable.SetValue("OPEN_STOCK", data.Open_Stock);
    //                        rfcTable.SetValue("SCAN_QTY", data.Scan_Qty);
    //                        rfcTable.SetValue("PICNR", data.PICNR);
    //                        rfcTable.SetValue("PICK_QTY", data.Pick_Qty);
    //                        rfcTable.SetValue("HU_NO", data.Hu_No);
    //                        rfcTable.SetValue("BARCODE", data.Barcode);
    //                        rfcTable.SetValue("MATKL", data.Matkl);
    //                        rfcTable.SetValue("WGBEZ", data.WGBEZ);
    //                        rfcTable.SetValue("SONUM", data.Sonum);
    //                        rfcTable.SetValue("DELNUM", data.Delnum);
    //                        rfcTable.SetValue("POSNR", data.Posnr);
    //                        rfcTable.SetValue("GNATURE", data.GNature);
    //                        rfcTable.SetValue("SAMMG", data.Sammg);
    //                        rfcTable.SetValue("PICK_STATUS", data.Pick_Status);
    //                    }

    //                    myfun.Invoke(dest);
    //                    //IRfcTable IrfTable = myfun.GetTable("ET_DATA");

    //                    IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");

    //                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
    //                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
    //                    if (SAP_TYPE == "E")
    //                    {
    //                        return Request.CreateResponse(HttpStatusCode.OK, new
    //                        {
    //                            Status = false,
    //                            Message = "" + SAP_Message + ""

    //                        });
    //                    }
    //                    return Request.CreateResponse(HttpStatusCode.OK, new
    //                    {
    //                        Status = true,
    //                        Message = "" + SAP_Message + ""

    //                    });
    //                }
    //                else
    //                {
    //                    return Request.CreateResponse(HttpStatusCode.OK, new
    //                    {
    //                        Status = true,
    //                        Message = "Request Not Valid"

    //                    });
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                return Request.CreateResponse(HttpStatusCode.InternalServerError, new
    //                {
    //                    Status = false,
    //                    Message = ex.Message
    //                });
    //            }
    //        });
    //    }
    //}
}