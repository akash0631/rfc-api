using DocumentFormat.OpenXml.Vml.Office;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Models;

namespace Vendor_Application_MVC.Controllers
{
    public class NSO_GL_CODESController : BaseController
    {

       
        public async Task<HttpResponseMessage> POST()
        {


            //return await Task.Run(() =>
            //{
            NSO_GL_CODES Authenticate = new NSO_GL_CODES();
            try
            {


                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZRFC_GL_CODE"); //RfcFunctionName
                                                                   //myfun.SetValue("IM_EBELN", request.PO); //Import Parameter
                                                                   //Import Parameter
                    myfun.Invoke(dest);


                    IRfcTable IrfTable = myfun.GetTable("GT_DATA");
                    IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
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
                        if (IrfTable.RowCount > 0)
                        {
                            for (int i = 0; i < IrfTable.RowCount; ++i)
                            {
                                NSO_GL_CODES_response gateEntryResponse = new NSO_GL_CODES_response();

                                gateEntryResponse.MANDT = IrfTable[i].GetString("MANDT");
                                gateEntryResponse.GL_ACC = IrfTable[i].GetString("GL_ACC");
                                gateEntryResponse.GL_TYPE = IrfTable[i].GetString("GL_TYPE");
                                gateEntryResponse.ZDESC = IrfTable[i].GetString("ZDESC");

                                Authenticate.Data.Add(gateEntryResponse);
                            }

                            Authenticate.Status = true;
                            Authenticate.Message = "Routing Status List fetch Successfully";
                            return Request.CreateResponse(HttpStatusCode.OK, Authenticate);

                        }
                        else
                        {
                            Authenticate.Status = false;
                            Authenticate.Message = "No Data found";
                            return Request.CreateResponse(HttpStatusCode.NotFound, Authenticate);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Authenticate.Status = false;
                    Authenticate.Message = "" + ex.Message + "";
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
                }

            }
            catch (Exception ex)
            {
                Authenticate.Status = false;
                Authenticate.Message = "" + ex.Message + "";
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
            }
            //});

        }
    }
    public class NSO_GL_CODES
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<NSO_GL_CODES_response> Data;
        public NSO_GL_CODES()
        {
            Data = new List<NSO_GL_CODES_response>();

        }
    }
    public class NSO_GL_CODES_response
    {
        public string MANDT { get; set; }
        public string GL_ACC { get; set; } = string.Empty;
        public string GL_TYPE { get; set; } = string.Empty;
        public string ZDESC { get; set; } = string.Empty;

    }

}