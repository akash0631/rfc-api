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
    public class zwm_dc_routing_rfcController : BaseController
    {

        public async Task<HttpResponseMessage> POST(string IM_GATE_ENTRY)
        {

           
            //return await Task.Run(() =>
            //{
                zwm_dc_routing_rfc Authenticate = new zwm_dc_routing_rfc();
                try
                {


                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZWM_DC_ROUTING_RFC"); //RfcFunctionName
                        myfun.SetValue("IM_GATE_ENTRY", IM_GATE_ENTRY); //Import Parameter
                                                                //Import Parameter
                        myfun.Invoke(dest);


                        IRfcTable IrfTable = myfun.GetTable("LT_DATA");
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
                                zwm_dc_routing_rfc_response gateEntryResponse = new zwm_dc_routing_rfc_response();

                                    gateEntryResponse.routingno = IrfTable[i].GetString("DCNO");
                                    gateEntryResponse.rout_desc = IrfTable[i].GetString("TEXT");

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
    public class zwm_dc_routing_rfc
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<zwm_dc_routing_rfc_response> Data;
        public zwm_dc_routing_rfc()
        {
            Data = new List<zwm_dc_routing_rfc_response>();

        }
    }
    public class zwm_dc_routing_rfc_response
    {
        public string routingno { get; set; }
        public string rout_desc { get; set; } = string.Empty;

    }
   
}