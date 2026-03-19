using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    /// <summary>
    /// RFC: ZVND_UNLOAD_HU_VALIDATE_RFC
    /// Purpose: HU validation RFC for Inbound Unloading Process.
    /// IMPORT: IM_USER (WWWOBJID), IM_PLANT (WERKS_D), IM_HU (ZEXT_HU)
    /// EXPORT: EX_RETURN (BAPIRET2), ET_DATA (ZTT_VEN_BOX)
    /// </summary>
    public class ZVND_UNLOAD_HU_VALIDATE_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZVND_UNLOAD_HU_VALIDATE_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;

                    IRfcFunction myfun = rfcrep.CreateFunction("ZVND_UNLOAD_HU_VALIDATE_RFC");

                    myfun.SetValue("IM_USER",  request.IM_USER);
                    myfun.SetValue("IM_PLANT", request.IM_PLANT);
                    myfun.SetValue("IM_HU",    request.IM_HU);

                    myfun.Invoke(dest);

                    IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");
                    string sapType    = exReturn.GetValue("TYPE").ToString();
                    string sapMessage = exReturn.GetValue("MESSAGE").ToString();

                    if (sapType == "E")
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = false,
                            Message = sapMessage
                        });
                    }

                    // Read ET_DATA table (ZTT_VEN_BOX)
                    IRfcTable etData = myfun.GetTable("ET_DATA");
                    var dataList = new List<object>();
                    for (int i = 0; i < etData.RowCount; i++)
                    {
                        etData.CurrentIndex = i;
                        dataList.Add(new
                        {
                            HU_NO       = etData.GetValue("HU_NO")?.ToString(),
                            PO_NO       = etData.GetValue("PO_NO")?.ToString(),
                            INV_NO      = etData.GetValue("INV_NO")?.ToString(),
                            VENDOR_CODE = etData.GetValue("VENDOR_CODE")?.ToString(),
                            VENDOR_NAME = etData.GetValue("VENDOR_NAME")?.ToString(),
                            ARTICLE_NO  = etData.GetValue("ARTICLE_NO")?.ToString(),
                            DESIGN      = etData.GetValue("DESIGN")?.ToString(),
                            QUANTITY    = etData.GetValue("QUANTITY")?.ToString(),
                            EAN         = etData.GetValue("EAN")?.ToString()
                        });
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status  = true,
                        Message = sapMessage,
                        Data    = dataList
                    });
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status  = false,
                        Message = ex.Message
                    });
                }
            });
        }
    }

    public class ZVND_UNLOAD_HU_VALIDATE_RFCRequest
    {
        public string IM_USER  { get; set; }   // WWWOBJID - logged-in user
        public string IM_PLANT { get; set; }   // WERKS_D  - plant/DC (auto-fetched)
        public string IM_HU    { get; set; }   // ZEXT_HU  - scanned HU number
    }
}
