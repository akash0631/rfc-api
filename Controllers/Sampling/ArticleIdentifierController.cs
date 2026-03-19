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
    public class Article_Request
    {
        public string Article_Number { get; set; } = String.Empty;

    }
    public class ArticleIdentifer
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public ArticleResponse Data;
        public ArticleIdentifer()
        {
            Data = new ArticleResponse();
        }
    }
   
    public class ArticleResponse
    {
        public string Vendor_Code { get; set; }
        public string Vendor_Name { get; set; }

        public string GRC_Date { get; set; } = String.Empty;
        public string GRC_Cost { get; set; } = String.Empty;
        public string GRC_Qty { get; set; } = String.Empty;
        public string Bill_No { get; set; } = String.Empty;
      
    }
    public class ArticleIdentifierController : BaseController
    {
        // GET: HUNameCheck
        [b.HttpPost]
        public async Task<HttpResponseMessage> POST(Article_Request request)
        {
            //Article_Request request = new Article_Request();
            //request.Article_Number = "112500893838008";
            return await Task.Run(() =>
            {
                try
                {
                    if (request.Article_Number != "" && request.Article_Number != null
                        )
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZEAN_ART_DETAILS"); //RfcFunctionName
                                                                           //IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                        myfun.SetValue("LV_ART", request.Article_Number);
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