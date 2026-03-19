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
    public class PaymentsrmController : BaseController
    {
        //public async Task<HttpResponseMessage> POST([FromBody] PO_DetailRequest request)
        public async Task<HttpResponseMessage> POST(Payment_Request request)
        {
            Payment PO_Detail = new Payment();
            //PO_DetailRequest request = new PO_DetailRequest();
                try
                {

                    if (request.vendorcode != "" && request.vendorcode != null)
                    {
                        return await Task.Run(() =>
                        {
                            try
                            {
                                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                                // Get RfcTable from SAP
                                RfcRepository rfcrep = dest.Repository;
                                IRfcFunction myfun = null;
                                myfun = rfcrep.CreateFunction("ZSRM_VEND_PAYMENT_INFO"); //RfcFunctionName
                                                                                 //myfun.SetValue("IM_USER", request.IM_PO); //Import Parameter
                                myfun.SetValue("IM_LIFNR","0000"+ request.vendorcode); //Import Parameter

                                myfun.Invoke(dest);


                                IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                                int iew=IrfTable.RowCount;
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
                                            PaymentResponse authenticateResponse = new PaymentResponse();
                                            authenticateResponse.Vendor_CD = IrfTable[i].GetString("LIFNR");
                                            authenticateResponse.Vendor_Name = IrfTable[i].GetString("NAME1");
                                            authenticateResponse.City = IrfTable[i].GetString("ORT01");
                                            authenticateResponse.Payment_Doc_DT = IrfTable[i].GetString("LAUFD");
                                            authenticateResponse.Payment_Doc_No = IrfTable[i].GetString("VBLNR");
                                            authenticateResponse.Amount = IrfTable[i].GetString("RBETR");
                                            authenticateResponse.PO_Number = IrfTable[i].GetString("EBELN");
                                            authenticateResponse.Delivery_Date = IrfTable[i].GetString("EINDT");
                                            authenticateResponse.Bill_No = IrfTable[i].GetString("INVNO");
                                            authenticateResponse.QTY = IrfTable[i].GetString("MENGE");
                                            authenticateResponse.Net_Value = IrfTable[i].GetString("NETWR");
                                            authenticateResponse.GR_Qty = IrfTable[i].GetString("GR_QTY");
                                            authenticateResponse.GR_Value = IrfTable[i].GetString("GR_VALUE");
                                            authenticateResponse.Gate_Entry_Number = IrfTable[i].GetString("EDOCNO");
                                            authenticateResponse.Gate_Entry_Date = IrfTable[i].GetString("EDATE");
                                            authenticateResponse.Gate_Entry_Time = IrfTable[i].GetString("ETIME");
                                            authenticateResponse.Invoice_Qty = IrfTable[i].GetString("INV_QTY");
                                            authenticateResponse.Invoice_Value = IrfTable[i].GetString("INV_VAL");


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
                        });

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