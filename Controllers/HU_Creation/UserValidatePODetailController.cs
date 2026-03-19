using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using VendorSRM_Application.Models;

namespace VendorSRM_Application.Controllers.API
{
    public class UserValidatePODetailController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> POST([FromBody] RoutingStatusListRequest request)
        {

            RoutingStatusList Authenticate = new RoutingStatusList();
            return await Task.Run(() =>
            {
                try
                {


                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZSRM_PO_DETAIL_FM"); //RfcFunctionName

                        myfun.SetValue("PO_NUM", request.PO_NUM); //Import Parameter

                        myfun.SetValue("VENDOR_CODE", request.VENDOR_CODE); //Import Parameter

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
                            if (IrfTable.RowCount > 0)
                            {
                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    RoutingStatusListResponse authenticateResponse = new RoutingStatusListResponse();
                                    authenticateResponse.PONUMBER = IrfTable[i].GetString("EBELN");
                                    authenticateResponse.ARTICLENUMBER = IrfTable[i].GetString("MATNR");
                                    authenticateResponse.DESIGN = IrfTable[i].GetString("ZEINR");
                                    authenticateResponse.QTY = IrfTable[i].GetString("MENGE");
                                    authenticateResponse.VENDORCODE = IrfTable[i].GetString("LIFNR");
                                    authenticateResponse.DOCUMENTNO = IrfTable[i].GetString("KNUMV");
                                    authenticateResponse.AMOUNT = IrfTable[i].GetString("NETWR");
                                    authenticateResponse.Date = IrfTable[i].GetString("AEDAT");
                                    authenticateResponse.EAN11 = IrfTable[i].GetString("EAN11");

                                    Authenticate.Data.Add(authenticateResponse);
                                }
                                Authenticate.Data = Authenticate.Data.Distinct().ToList();
                                Authenticate.Status = true;
                                Authenticate.Message = "PO Pending List fetch Successfully";
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
            });


        }
    }
}