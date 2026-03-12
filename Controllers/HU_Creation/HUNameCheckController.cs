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
using b= System.Web.Mvc;
using VendorSRM_Application.Models;
using Vendor_Application_MVC.Controllers;

namespace VendorSRM_Application.Controllers.API
{
    public class HUNameCheckController : BaseController
    {
        // GET: HUNameCheck
        [b.HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] Submit_HU_Details request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request.HU_NO != "" && request.HU_NO != null &&
                        request.PO_NO != "" && request.PO_NO != null
                        )
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZVND_HU_CHECK_RFC"); //RfcFunctionName
                        IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                        E_Data.SetValue("HU_NO", request.HU_NO.ToUpper());
                        E_Data.SetValue("PO_NO", request.PO_NO);


                        myfun.Invoke(dest);
                        IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                        string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                        if (SAP_TYPE == "F")
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = false,
                                Message = "" + SAP_Message + "",
                                IsValid = false
                            });
                        }

                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = true,
                            Message = "" + SAP_Message + "",
                            IsValid = true,
                        });
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status = false,
                            Message = "All request field is Mandatory.",
                            IsValid = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status = false,
                        Message = ex.Message,
                        IsValid = false,
                    });
                }
            });
        }
    }
}