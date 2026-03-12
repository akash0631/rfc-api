using SAP.Middleware.Connector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.UI.WebControls;
using Vendor_Application_MVC.Models;

namespace Vendor_Application_MVC.Controllers
{
    public class ZESIC_MASTER_POST_RFCController : BaseController
    {
        

        public async Task<HttpResponseMessage> POST([FromBody] ZESIC_MASTER_POST_RFCRequest request)
        {
            ZESIC_MASTER_POST_RFC Authenticate = new ZESIC_MASTER_POST_RFC();

            try
            {

                if (request.IM_ST_CD != "" && request.IM_ST_CD != "")
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZESIC_MASTER_POST_RFC"); //RfcFunctionName
                                                                                //IRfcStructure rfcFields = myfun.GetStructure("LS_GET_DATA");
                        myfun.SetValue("IM_ST_CD", request.IM_ST_CD);
                        myfun.SetValue("IM_STATUS", request.IM_STATUS);
                        myfun.SetValue("IM_ST_ESIC_CD", request.IM_ST_ESIC_CD);
                        myfun.SetValue("IM_ST_ESIC_CD_REF", request.IM_ST_ESIC_CD_REF);

                        myfun.Invoke(dest);


                        
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

                          
                            //Distinct(row).ToList();
                            Authenticate.Status = true;
                            Authenticate.Message = "" + SAP_Message + "";
                            return Request.CreateResponse(HttpStatusCode.OK, Authenticate);
                           
                        }


                    }
                    catch (Exception ex)
                    {
                        Authenticate.Status = false;
                        Authenticate.Message = "" + ex.Message + "";
                        return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
                    }
                }
                else
                {
                    Authenticate.Status = false;
                    Authenticate.Message = "Username and Password is Mandatory.";
                    return Request.CreateResponse(HttpStatusCode.BadRequest, Authenticate);
                }
            }
            catch (Exception ex)
            {
                Authenticate.Status = false;
                Authenticate.Message = "" + ex.Message + "";
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
            }


        }
       
    }




    public class ZESIC_MASTER_POST_RFC
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public string StoreId { get; set; }
        public List<ZESIC_MASTER_POST_RFCResponse> Data;
        public ZESIC_MASTER_POST_RFC()
        {
            Data = new List<ZESIC_MASTER_POST_RFCResponse>();
        }
    }
    public class ZESIC_MASTER_POST_RFCRequest
    {
        public string IM_ST_CD { get; set; }
        public string IM_STATUS { get; set; }
        public string IM_ST_ESIC_CD { get; set; }
        public string IM_ST_ESIC_CD_REF { get; set; }



    }
    public class ZESIC_MASTER_POST_RFCResponse
    {
        public string Vendor_Code { get; set; }
        public string Vendor_Name { get; set; }
        public string PO_Number { get; set; }
        public string Date { get; set; } = String.Empty;
        public string DeliveryDate { get; set; } = String.Empty;
        public string POQty { get; set; } = String.Empty;
    }
}
