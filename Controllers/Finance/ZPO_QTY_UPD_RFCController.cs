using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZPO_QTY_UPD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZPO_QTY_UPD_RFC/Post")]
        public IHttpActionResult Post([FromBody] ZPO_QTY_UPD_RFCRequest request)
        {
            try
            {
                if (request == null)
                    return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = "Request data is required" });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("ZPO_QTY_UPD_RFC");

                if (request.IM_DATA != null && request.IM_DATA.Count > 0)
                {
                    IRfcTable imDataTable = myfun.GetTable("IM_DATA");
                    foreach (var row in request.IM_DATA)
                    {
                        imDataTable.Append();
                        if (!string.IsNullOrEmpty(row.EBELN)) imDataTable.SetValue("EBELN", row.EBELN);
                        if (!string.IsNullOrEmpty(row.MATNR)) imDataTable.SetValue("MATNR", row.MATNR);
                        if (!string.IsNullOrEmpty(row.QTY))   imDataTable.SetValue("QTY", row.QTY);
                    }
                }

                myfun.Invoke(dest);

                string msgType = myfun.GetString("MSG_TYPE");
                string message = myfun.GetString("MESSAGE");

                return Ok(new { Status = msgType == "S", MSG_TYPE = msgType, MESSAGE = message });
            }
            catch (RfcAbapException ex)      { return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message }); }
            catch (RfcCommunicationException ex) { return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message }); }
            catch (Exception ex)             { return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message }); }
        }
    }

    public class ZPO_QTY_UPD_RFCRequest
    {
        public List<ZST_PO_IMP_QTY> IM_DATA { get; set; }
    }

    public class ZST_PO_IMP_QTY
    {
        public string EBELN { get; set; }  // PO Number
        public string MATNR { get; set; }  // Material Number
        public string QTY   { get; set; }  // Quantity
    }
}
