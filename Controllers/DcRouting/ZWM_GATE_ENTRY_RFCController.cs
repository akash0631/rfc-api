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
    public class ZWM_GATE_ENTRY_RFCController : BaseController
    {

        public async Task<HttpResponseMessage> POST()
        {

            return await Task.Run(() =>
            {

                ZWM_GATE_ENTRY_RFC Authenticate = new ZWM_GATE_ENTRY_RFC();
                try
                {


                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZWM_GATE_ENTRY_RFC"); //RfcFunctionName
                        //myfun.SetValue("IM_EBELN", request.PO); //Import Parameter
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
                                    ZWM_GATE_ENTRY_RFC_response gateEntryResponse = new ZWM_GATE_ENTRY_RFC_response();

                                    gateEntryResponse.Gate_Entry_No = IrfTable[i].GetString("EDOCNO");
                                    gateEntryResponse.Quantity = IrfTable[i].GetString("LOT_QTY");
                                    gateEntryResponse.Lot_Ageing = IrfTable[i].GetString("LOT_AG");
                                    gateEntryResponse.Pending_Lot_Qty = IrfTable[i].GetString("PENDING_LOT_QTY");
                                    gateEntryResponse.Process_Ageing = IrfTable[i].GetString("PRO_AG");
                                    gateEntryResponse.PO_NO = IrfTable[i].GetString("PONO");
                                    Authenticate.Data.Add(gateEntryResponse);
                                }

                                Authenticate.Status = true;
                                Authenticate.Message = "Gate entry  List fetch Successfully";
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
    public class ZWM_GATE_ENTRY_RFC
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<ZWM_GATE_ENTRY_RFC_response> Data;
        public ZWM_GATE_ENTRY_RFC()
        {
            Data = new List<ZWM_GATE_ENTRY_RFC_response>();

        }
    }
    public class ZWM_GATE_ENTRY_RFC_response
    {
        public string Gate_Entry_No { get; set; }
        public string Quantity { get; set; } = string.Empty;
        public string Lot_Ageing { get; set; } = string.Empty;
        public string Process_Ageing { get; set; } = string.Empty;
        public string PO_NO { get; set; } = string.Empty;
        public string Pending_Lot_Qty { get;  set; } = string.Empty ;
    }
   
}