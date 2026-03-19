using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office.CustomUI;
using DocumentFormat.OpenXml.Office2016.Excel;
using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;

namespace Vendor_SRM_Routing_Application.Controllers.PeperlessPicklist
{
    public class PicklistResponse
    {
        public string WM_NO { get; set; }
        public string MATERIAL { get; set; }
        public string PLANT { get; set; }
        public string STOR_LOC { get; set; }
        public string BATCH { get; set; }
        public string CRATE { get; set; }
        public string BIN { get; set; }
        public string STORAGE_TYPE { get; set; }
        public string MEINS { get; set; }
        public string AVL_STOCK { get; set; }
        public string OPEN_STOCK { get; set; }
        public string SCAN_QTY { get; set; }
        public string PICNR { get; set; }
        public string PICK_QTY { get; set; }
        public string HU_NO { get; set; }
        public string BARCODE { get; set; }
        public string MATKL { get; set; }
        public string WGBEZ { get; set; }
        public string SONUM { get; set; }
        public string DELNUM { get; set; }
        public string POSNR { get; set; }
        public string GNATURE { get; set; }
        public string SAMMG { get; set; }
        public string PICK_STATUS { get; set; }
    }

    public class GetPicklistNumberController: BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] GetPicklistRequest request)
        {
            //request.IM_DATUM = DateTime.Now;
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if (request.IM_USER != null && request.IM_WERKS != null)
                    {

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZWM_RFC_GET_PICKLIST");

                        myfun.SetValue("IM_USER", request.IM_USER);
                        myfun.SetValue("IM_DATUM", request.IM_DATUM);
                        myfun.SetValue("IM_WERKS", request.IM_WERKS);

                        myfun.Invoke(dest);
                        IRfcTable IrfTable = myfun.GetTable("ET_DATA");


                        var picnrDataList = IrfTable.AsEnumerable().Select(row => row["PICNR"].GetValue()).ToList();
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

                        //    var picnrList = new List<PicklistResponse>();

                        //    foreach(var item in IrfTable)
                        //{
                        //    var _pickListResponse = new PicklistResponse();

                        //    _pickListResponse.WM_NO = item.GetString("WM_NO").ToString();
                        //    _pickListResponse.MATERIAL = item.GetString("MATERIAL").ToString();
                        //    _pickListResponse.PLANT = item.GetString("PLANT").ToString();
                        //    _pickListResponse.STOR_LOC = item.GetString("STOR_LOC").ToString();
                        //    _pickListResponse.BATCH = item.GetString("BATCH").ToString();
                        //    _pickListResponse.CRATE = item.GetString("CRATE").ToString();
                        //    _pickListResponse.BIN = item.GetString("BIN").ToString();
                        //    _pickListResponse.STORAGE_TYPE = item.GetString("STORAGE_TYPE").ToString();
                        //    _pickListResponse.MEINS = item.GetString("MEINS").ToString();
                        //    _pickListResponse.AVL_STOCK = item.GetString("AVL_STOCK").ToString();
                        //    _pickListResponse.OPEN_STOCK = item.GetString("OPEN_STOCK").ToString();
                        //    _pickListResponse.SCAN_QTY = item.GetString("SCAN_QTY").ToString();
                        //    _pickListResponse.SCAN_QTY = item.GetString("SCAN_QTY").ToString();
                        //    _pickListResponse.PICNR = item.GetString("PICNR").ToString();
                        //    _pickListResponse.PICK_QTY = item.GetString("PICK_QTY").ToString();
                        //    _pickListResponse.HU_NO = item.GetString("HU_NO").ToString();
                        //    _pickListResponse.BARCODE = item.GetString("BARCODE").ToString();
                        //    _pickListResponse.MATKL = item.GetString("MATKL").ToString();
                        //    _pickListResponse.WGBEZ = item.GetString("WGBEZ").ToString();
                        //    _pickListResponse.SONUM = item.GetString("SONUM").ToString();
                        //    _pickListResponse.DELNUM = item.GetString("DELNUM").ToString();
                        //    _pickListResponse.POSNR = item.GetString("POSNR").ToString();
                        //    _pickListResponse.GNATURE = item.GetString("GNATURE").ToString();
                        //    _pickListResponse.SAMMG = item.GetString("SAMMG").ToString();
                        //    _pickListResponse.PICK_STATUS = item.GetString("PICK_STATUS").ToString();

                        //    picnrList.Add(_pickListResponse);
                        //}

                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = true,
                            Message = "" + SAP_Message + "",
                            Data = picnrDataList
                        });
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
}