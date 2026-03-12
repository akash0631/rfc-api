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
    public class PRvsPOController : BaseController
    {

        public async Task<HttpResponseMessage> POST(PRvsPORequest request)
        {
            
            PRvsPO Authenticate = new PRvsPO();
            return await Task.Run(() =>
            {
                try
                {


                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZPUR_TREND_DATA"); //RfcFunctionName
                        myfun.SetValue("I_GJAHR", request.Year.Trim()); //Import Parameter
                        myfun.SetValue("I_MATCAT", request.Majcat.Trim()); //Import Parameter
                        myfun.Invoke(dest);


                        IRfcTable IrfTable = myfun.GetTable("ET_DAT");
                        //IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                        //string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                        //string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                        //if (SAP_TYPE == "E")
                        //{
                        //    Authenticate.Status = false;
                        //    Authenticate.Message = "" + SAP_Message + "";
                        //    return Request.CreateResponse(HttpStatusCode.BadRequest, Authenticate);
                        //}
                        //else
                        //{
                        if (IrfTable.RowCount > 0)
                        {
                            for (int i = 0; i < IrfTable.RowCount; ++i)
                            {
                                PRvsPOResponse authenticateResponse = new PRvsPOResponse();
                                authenticateResponse.MAJ_CAT_CD = IrfTable[i].GetString("MAJ_CAT_CD").Trim();
                                authenticateResponse.MATCAT = IrfTable[i].GetString("MATCAT").Trim();
                                authenticateResponse.JAN_QTY_PR = IrfTable[i].GetString("JAN_QTY_PR").Trim();
                                authenticateResponse.JAN_QTY_PO = IrfTable[i].GetString("JAN_QTY_PO").Trim();
                                authenticateResponse.JAN_QTY_DIFF = IrfTable[i].GetString("JAN_QTY_DIFF").Trim();
                                authenticateResponse.FEB_QTY_PR = IrfTable[i].GetString("FEB_QTY_PR").Trim();
                                authenticateResponse.FEB_QTY_PO = IrfTable[i].GetString("FEB_QTY_PO").Trim();
                                authenticateResponse.FEB_QTY_DIFF = IrfTable[i].GetString("FEB_QTY_DIFF").Trim();
                                authenticateResponse.MAR_QTY_PR = IrfTable[i].GetString("MAR_QTY_PR").Trim();
                                authenticateResponse.MAR_QTY_PO = IrfTable[i].GetString("MAR_QTY_PO").Trim();
                                authenticateResponse.MAR_QTY_DIFF = IrfTable[i].GetString("MAR_QTY_DIFF").Trim();
                                authenticateResponse.APR_QTY_PR = IrfTable[i].GetString("APR_QTY_PR").Trim();
                                authenticateResponse.APR_QTY_PO = IrfTable[i].GetString("APR_QTY_PO").Trim();
                                authenticateResponse.APR_QTY_DIFF = IrfTable[i].GetString("APR_QTY_DIFF").Trim();
                                authenticateResponse.MAY_QTY_PR = IrfTable[i].GetString("MAY_QTY_PR").Trim();
                                authenticateResponse.MAY_QTY_PO = IrfTable[i].GetString("MAY_QTY_PO").Trim();
                                authenticateResponse.MAY_QTY_DIFF = IrfTable[i].GetString("MAY_QTY_DIFF").Trim();
                                authenticateResponse.JUN_QTY_PR = IrfTable[i].GetString("JUN_QTY_PR").Trim();
                                authenticateResponse.JUN_QTY_PO = IrfTable[i].GetString("JUN_QTY_PO").Trim();
                                authenticateResponse.JUN_QTY_DIFF = IrfTable[i].GetString("JUN_QTY_DIFF").Trim();
                                authenticateResponse.JUL_QTY_PR = IrfTable[i].GetString("JUL_QTY_PR").Trim();
                                authenticateResponse.JUL_QTY_PO = IrfTable[i].GetString("JUL_QTY_PO").Trim();
                                authenticateResponse.JUL_QTY_DIFF = IrfTable[i].GetString("JUL_QTY_DIFF").Trim();
                                authenticateResponse.AUG_QTY_PR = IrfTable[i].GetString("AUG_QTY_PR").Trim();
                                authenticateResponse.AUG_QTY_PO = IrfTable[i].GetString("AUG_QTY_PO").Trim();
                                authenticateResponse.AUG_QTY_DIFF = IrfTable[i].GetString("AUG_QTY_DIFF").Trim();
                                authenticateResponse.SEP_QTY_PR = IrfTable[i].GetString("SEP_QTY_PR").Trim();
                                authenticateResponse.SEP_QTY_PO = IrfTable[i].GetString("SEP_QTY_PO").Trim();
                                authenticateResponse.SEP_QTY_DIFF = IrfTable[i].GetString("SEP_QTY_DIFF").Trim();
                                authenticateResponse.OCT_QTY_PR = IrfTable[i].GetString("OCT_QTY_PR").Trim();
                                authenticateResponse.OCT_QTY_PO = IrfTable[i].GetString("OCT_QTY_PO").Trim();
                                authenticateResponse.OCT_QTY_DIFF = IrfTable[i].GetString("OCT_QTY_DIFF").Trim();
                                authenticateResponse.NOV_QTY_PR = IrfTable[i].GetString("NOV_QTY_PR").Trim();
                                authenticateResponse.NOV_QTY_PO = IrfTable[i].GetString("NOV_QTY_PO").Trim();
                                authenticateResponse.NOV_QTY_DIFF = IrfTable[i].GetString("NOV_QTY_DIFF").Trim();
                                authenticateResponse.DEC_QTY_PR = IrfTable[i].GetString("DEC_QTY_PR").Trim();
                                authenticateResponse.DEC_QTY_PO = IrfTable[i].GetString("DEC_QTY_PO").Trim();
                                authenticateResponse.DEC_QTY_DIFF = IrfTable[i].GetString("DEC_QTY_DIFF").Trim();
                                Authenticate.Data.Add(authenticateResponse);
                            }

                            Authenticate.Status = true;
                            Authenticate.Message = "PO VS PR List fetch Successfully";
                            return Request.CreateResponse(HttpStatusCode.OK, Authenticate);

                        }
                        else
                        {
                            Authenticate.Status = false;
                            Authenticate.Message = "No Data found";
                            return Request.CreateResponse(HttpStatusCode.NotFound, Authenticate);
                        }
                        //}

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
            });


        }
    }
}