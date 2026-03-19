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
    public class GateEntryDetailsController : BaseController
    {

        public async Task<HttpResponseMessage> POST(GateEntryRequest request)
        {
            
            GateEntryList Authenticate = new GateEntryList();
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
                        myfun = rfcrep.CreateFunction("ZSRM_GATE_ENTRY_DETAILS"); //RfcFunctionName
                        myfun.SetValue("IM_EBELN", request.PO); //Import Parameter
                                                                //Import Parameter
                        myfun.Invoke(dest);


                        IRfcTable IrfTable = myfun.GetTable("ET_DATA");
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
                                    GateEntryResponse gateEntryResponse = new GateEntryResponse();

                                    gateEntryResponse.VendorCode = IrfTable[i].GetString("LIFNR");
                                    gateEntryResponse.VendorName = IrfTable[i].GetString("NAME1");
                                    gateEntryResponse.City = IrfTable[i].GetString("ORT01");
                                    gateEntryResponse.PoNumber = IrfTable[i].GetString("EBELN");
                                    gateEntryResponse.PoQty = IrfTable[i].GetString("MENGE");
                                    gateEntryResponse.PoValue = IrfTable[i].GetString("NETWR");
                                    gateEntryResponse.PoDelDate = IrfTable[i].GetString("EINDT");
                                    gateEntryResponse.GeNo = IrfTable[i].GetString("EDOCNO");
                                    gateEntryResponse.GeDate = IrfTable[i].GetString("EDATE");
                                    gateEntryResponse.BillNo = IrfTable[i].GetString("INVNO");
                                    gateEntryResponse.BillQty = IrfTable[i].GetString("INV_QTY");
                                    gateEntryResponse.BillVal = IrfTable[i].GetString("INV_VAL");
                                    gateEntryResponse.DiffQty = IrfTable[i].GetString("DIFF_QTY");
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
            });

        }
    }
}