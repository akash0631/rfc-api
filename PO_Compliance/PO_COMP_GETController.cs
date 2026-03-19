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
    public class PO_COMP_GETController : BaseController
    {
        public async Task<HttpResponseMessage> POST([FromBody] PO_DetailRequest request)
        {
            PO_COMP_POST PO_Detail = new PO_COMP_POST();

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
                        myfun = rfcrep.CreateFunction("ZSRM_PO_COMP_GET"); //RfcFunctionName
                        //myfun = rfcrep.CreateFunction("ZSRM_PO_COMP_LIST"); //RfcFunctionName
                        //myfun.SetValue("IM_USER", request.IM_PO); //Import Parameter
                        myfun.SetValue("IM_EBLN", request.IM_PO); //Import Parameter

                        myfun.Invoke(dest);


                        IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                        //IRfcTable IrfTable = myfun.GetTable("ET_PO_COMP_LIST");
                        //IRfcStructure IrfTable = myfun.GetStructure("IM_DATA");
                        //IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
                        IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
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
                                    PO_DetailResponseCOMP authenticateResponse = new PO_DetailResponseCOMP();
                                    authenticateResponse.MANDT = IrfTable[i].GetString("MANDT");
                                    authenticateResponse.SNO = IrfTable[i].GetString("SNO");
                                    authenticateResponse.FIELD_NAME = IrfTable[i].GetString("FILED_NAME");
                                    authenticateResponse.FIELD_VALUE = IrfTable[i].GetString("FIELD_VALUE");
                       

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

                            //PO_Detail.Status = false;
                            //PO_Detail.Message = "No Data found";
                            //return Request.CreateResponse(HttpStatusCode.NotFound, PO_Detail);

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