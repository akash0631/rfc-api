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
    public class ZVND_HU_PUSH_API_POSTController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] List<ZVND_HU_PUSH_API_POSTRequest> request1)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                   

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZVND_HU_PUSH_API_POST");

                        IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                    foreach (var request in request1)
                    {
                      
                        E_Data.SetValue("HU_NO", request.HU_NO);
                        E_Data.SetValue("PO_NO", request.PO_NO);
                        E_Data.SetValue("ARTICLE_NO", request.ARTICLE_NO);
                        E_Data.SetValue("DESIGN", request.DESIGN);
                        E_Data.SetValue("QUANTITY", request.QUANTITY);
                        E_Data.SetValue("VENDOR_CODE", request.VENDOR_CODE);
                        E_Data.SetValue("EAN", request.EAN);
                        E_Data.SetValue("CREATION_DATE", request.CREATION_DATE);
                        E_Data.SetValue("CREATION_TIME", request.CREATION_TIME);
                        E_Data.SetValue("CREATION_USER", request.CREATION_USER);
                        E_Data.SetValue("MESSAGE", request.MESSAGE);
                        E_Data.SetValue("STATUS", request.STATUS);
                        E_Data.SetValue("INV_NO", request.INV_NO);

                    }



                        myfun.Invoke(dest);
                        IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                        string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                        if (SAP_TYPE == "E")
                        {
                            //PO_Detail.Status = false;
                            //PO_Detail.Message = "" + SAP_Message + "";
                            return Request.CreateResponse(HttpStatusCode.BadRequest, new
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
    public class ZVND_HU_PUSH_API_POSTRequest
    {
        public string HU_NO { get; set; }
        public string PO_NO { get; set; }
        public string ARTICLE_NO { get; set; }
        public string DESIGN { get; set; }
        public string QUANTITY { get; set; }
        public string VENDOR_CODE { get; set; }
        public string EAN { get; set; }
        public string CREATION_DATE { get; set; }
        public string CREATION_TIME { get; set; }
        public string CREATION_USER { get; set; }
        public string MESSAGE { get; set; }
        public string STATUS { get; set; }
        public string INV_NO { get; set; }
        


    }
}