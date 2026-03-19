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
    public class ZVND_HU_VALIDATE_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZVND_HU_VALIDATE_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if (request.IM_USER != null)
                    {

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZVND_HU_VALIDATE_RFC");

                        
                        myfun.SetValue("IM_USER", request.IM_USER);
                        myfun.SetValue("IM_HU_NUMBER", request.IM_HU_NUMBER);
                        myfun.SetValue("IM_PO", request.IM_PO);



                        myfun.Invoke(dest);
                      //  IRfcTable IrfTable = myfun.GetTable("ET_STORES");
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
    public class ZVND_HU_VALIDATE_RFCRequest
    {
        public string IM_USER { get; set; }
        
        public string IM_HU_NUMBER { get; set; }
        public string IM_PO { get; set; }


    }
}