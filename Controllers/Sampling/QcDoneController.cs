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
using Vendor_Application_MVC.Models;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Sampling.Controllers.API
{

    public class QcDoneController : BaseController
    {
        // GET: HUNameCheck
        [b.HttpGet]
        //public async Task<HttpResponseMessage> Post([FromBody] Article_Request request)
        public async Task<HttpResponseMessage> Get()
        {
            return await Task.Run(() =>
            {
                try
                {
                    //if (request.Article_Number != "" && request.Article_Number != null
                    //    )
                    //{
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZQCDONE_RFC"); //RfcFunctionName
                    //IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                    //myfun.SetValue("LV_ART", request.Article_Number);
                    //E_Data.SetValue("HU_NO", request.HU_NO.ToUpper());

                    ArticleIdentifer Authenticate = new ArticleIdentifer();

                    myfun.Invoke(dest);
                    IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                    IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                    if (SAP_TYPE == "E")
                    {
                        Authenticate.Status = false;
                        Authenticate.Message = "" + SAP_Message + "";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, Authenticate);
                    }
                    else
                    {
                        ArticleResponse authenticateResponse = new ArticleResponse();

                        for (int i = 0; i < IrfTable.RowCount; ++i)
                        {



                            authenticateResponse.Vendor_Code = IrfTable[i].GetString("LIFNR");
                            authenticateResponse.Vendor_Name = IrfTable[i].GetString("NAME1");
                            authenticateResponse.GRC_Date = IrfTable[i].GetString("BUDAT_MKPF");
                            authenticateResponse.GRC_Cost = IrfTable[i].GetString("DMBTR");
                            authenticateResponse.GRC_Qty = IrfTable[i].GetString("MENGE");
                            authenticateResponse.Bill_No = IrfTable[i].GetString("MBLNR");





                        }
                        Authenticate.Data = authenticateResponse;
                        Authenticate.Status = true;
                        Authenticate.Message = "" + SAP_Message + "";
                        return Request.CreateResponse(HttpStatusCode.OK, Authenticate);

                    }


                    //}
                    //else
                    //{
                    //    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    //    {
                    //        Status = false,
                    //        Message = "All request field is Mandatory.",
                    //        IsValid = false
                    //    });
                    //}
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