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
                        // PO_ITEM is optional — defaults to "00010" (first SAP line item) if not provided
                        string poItem = !string.IsNullOrEmpty(row.PO_ITEM) ? row.PO_ITEM : "00010";
                        imDataTable.SetValue("PO_ITEM", poItem);
                        if (!string.IsNullOrEmpty(row.QTY)) imDataTable.SetValue("QTY", row.QTY);
                    }
                }

                myfun.Invoke(dest);

                string msgType = myfun.GetString("MSG_TYPE");
                string message = myfun.GetString("MESSAGE");

                return Ok(new { Status = msgType == "S", MSG_TYPE = msgType, MESSAGE = message });
            }
            catch (RfcAbapException ex)          { return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message }); }
            catch (RfcCommunicationException ex) { return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message }); }
            catch (Exception ex)                 { return Ok(new { Status = false, MSG_TYPE = "E", MESSAGE = ex.Message }); }
        }
    }

    public class ZPO_QTY_UPD_RFCRequest
    {
        public List<ZST_PO_IMP_QTY> IM_DATA { get; set; }
    }

    public class ZST_PO_IMP_QTY
    {
        public string EBELN   { get; set; }  // PO Number (CHAR 10) — required
        public string MATNR   { get; set; }  // Material Number (CHAR 40) — required
        public string QTY     { get; set; }  // Quantity (CHAR 13) — required
        public string PO_ITEM { get; set; }  // Line Item (NUMC 5) — optional, defaults to 00010
    }
}
