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

namespace Vendor_SRM_Routing_Application.Controllers.Vehicle_Loading
{
    public class ZWM_VEH_TAT_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post(ZWM_VEH_TAT_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // DEV (174) for initial smoke. Swap to rfcConfigparametersquality() after STMS to QA, then rfcConfigparametersproduction() after STMS to PROD.
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    RfcRepository rfcrep = dest.Repository;

                    IRfcFunction myfun = rfcrep.CreateFunction("ZWM_VEH_TAT_RFC");

                    myfun.SetValue("IM_DATE_FROM", request.IM_DATE_FROM ?? "");
                    myfun.SetValue("IM_DATE_TO", request.IM_DATE_TO ?? "");
                    myfun.SetValue("IM_SENDING_SITE", request.IM_SENDING_SITE ?? "");
                    myfun.SetValue("IM_RECEIVING_SITE", request.IM_RECEIVING_SITE ?? "");
                    myfun.SetValue("IM_VEHICLE_NO", request.IM_VEHICLE_NO ?? "");
                    myfun.SetValue("IM_JOURNEY_NO", request.IM_JOURNEY_NO ?? "");

                    myfun.Invoke(dest);

                    IRfcStructure E_RETURN = myfun.GetStructure("ET_RETURN");
                    IRfcTable IrfTable = myfun.GetTable("ET_DATA");

                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();

                    if (SAP_TYPE == "E")
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = false,
                            Message = SAP_Message
                        });
                    }

                    var etdata = IrfTable.AsEnumerable().Select(row => new
                    {
                        JOURNEY_NO = row.GetString("JOURNEY_NO"),
                        SENDING_SITE = row.GetString("SENDING_SITE"),
                        RECEIVING_SITE = row.GetString("RECEIVING_SITE"),
                        VEHICLE_NO = row.GetString("VEHICLE_NO"),
                        TRANSPORTER_NAME = row.GetString("TRANSPORTER_NAME"),
                        DRIVER_NAME = row.GetString("DRIVER_NAME"),
                        ENTRY_DATE = row.GetString("ENTRY_DATE"),
                        EXIT_DATE = row.GetString("EXIT_DATE"),
                        RECV_SITE_NAME = row.GetString("RECV_SITE_NAME"),
                        TAT_HOURS = row.GetString("TAT_HOURS"),
                        STATUS = row.GetString("STATUS")
                    }).ToList();

                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = true,
                        Message = SAP_Message,
                        Data = new
                        {
                            ET_Data = etdata
                        }
                    });
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

    public class ZWM_VEH_TAT_RFCRequest
    {
        public string IM_DATE_FROM { get; set; }
        public string IM_DATE_TO { get; set; }
        public string IM_SENDING_SITE { get; set; }
        public string IM_RECEIVING_SITE { get; set; }
        public string IM_VEHICLE_NO { get; set; }
        public string IM_JOURNEY_NO { get; set; }
    }
}
