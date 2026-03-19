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
    public class Zone_PO_DetailController : BaseController
    {
        public async Task<HttpResponseMessage> POST([FromBody] PO_DetailRequest request)
        {
            PO_Detail PO_Detail = new PO_Detail();

            try
            {

                if (request.IM_PO != "" && request.IM_PO != null)
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZSRM_PO_DETAIL"); //RfcFunctionName
                        //myfun.SetValue("IM_USER", request.IM_PO); //Import Parameter
                        myfun.SetValue("IM_PO", request.IM_PO); //Import Parameter

                        myfun.Invoke(dest);


                        IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                        IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                        string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                        if (SAP_TYPE == "E")
                        {
                            PO_Detail.Status = false;
                            PO_Detail.Message = "" + SAP_Message + "";
                            return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                        }
                        else
                        {
                            if (IrfTable.RowCount > 0)
                            {
                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    PO_DetailResponse authenticateResponse = new PO_DetailResponse();
                                    authenticateResponse.PO_NO = IrfTable[i].GetString("PO_NO");
                                    authenticateResponse.Maj_Cat = IrfTable[i].GetString("MAJ_CAT");
                                    authenticateResponse.Design_No = IrfTable[i].GetString("DESIGN_NO");
                                    authenticateResponse.Qty = IrfTable[i].GetString("QTY");
                                    authenticateResponse.Maj_Desc = IrfTable[i].GetString("WGBEZ");
                                    authenticateResponse.ArticleNo = IrfTable[i].GetString("SATNR");
                                    authenticateResponse.PO_Creation = IrfTable[i].GetString("AEDAT");
                                    authenticateResponse.PO_Delivery = IrfTable[i].GetString("EINDT");
                                    authenticateResponse.CurrentStatus = IrfTable[i].GetString("CURR_STATUS");

                                    // AEDAT -> PO Creation Date
                                    //EINDT -> PO Delivery Date
                                    PO_Detail.Data.Add(authenticateResponse);
                                }

                                PO_Detail.Status = true;
                                PO_Detail.Message = "" + SAP_Message + "";
                                return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                            }
                            else
                            {
                                PO_Detail.Status = false;
                                PO_Detail.Message = "No Data found";
                                return Request.CreateResponse(HttpStatusCode.NotFound, PO_Detail);
                            }

                           

                        }


                    }
                    catch (Exception ex)
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = "" + ex.Message + "";
                        return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);
                    }
                    
                    
                }
                else
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "PO Number is Mandatory.";
                    return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                }
            }
            catch (Exception ex)
            {
                PO_Detail.Status = false;
                PO_Detail.Message = "" + ex.Message + "";
                return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);
            }


        }
        
    }

}