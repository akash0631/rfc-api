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
    public class ZLWF_MASTER_POST_RFCController : BaseController
    {
        

        public async Task<HttpResponseMessage> POST([FromBody] ZLWF_MASTER_POST_RFCRequest request)
        {
            ZLWF_MASTER_POST_RFC Authenticate = new ZLWF_MASTER_POST_RFC();

            try
            {

                if (request.ST_CD != "" && request.ST_CD != "" && request.APPLICABLE != null && request.APPLICABLE != null)
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZLWF_MASTER_POST_RFC"); //RfcFunctionName
                        IRfcStructure rfcFields = myfun.GetStructure("LS_GET_DATA");
                        rfcFields.SetValue("ST_CD", request.ST_CD);
                        rfcFields.SetValue("APPLICABLE", request.APPLICABLE);
                        rfcFields.SetValue("STATUS", request.STATUS);
                        rfcFields.SetValue("LWF_SITE_CODE_REF", request.LWF_SITE_CODE_REF);
                        rfcFields.SetValue("TOTAL_AMOUNT", request.TOTAL_AMOUNT);
                        rfcFields.SetValue("EMPLOYEE_TTL_CNTB", request.EMPLOYEE_TTL_CNTB);
                        rfcFields.SetValue("EMPLOYEE_MON_CNTB", request.EMPLOYEE_MON_CNTB);
                        rfcFields.SetValue("EMPLOYER_TTL_CNTB", request.EMPLOYER_TTL_CNTB);
                        rfcFields.SetValue("EMPLOYER_MON_CNTB", request.EMPLOYER_MON_CNTB);
                        rfcFields.SetValue("DEPOSITE_FREQ", request.DEPOSITE_FREQ);

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




    public class ZLWF_MASTER_POST_RFC
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public string StoreId { get; set; }
        public List<ZLWF_MASTER_POST_RFCResponse> Data;
        public ZLWF_MASTER_POST_RFC()
        {
            Data = new List<ZLWF_MASTER_POST_RFCResponse>();
        }
    }
    public class ZLWF_MASTER_POST_RFCRequest
    {
        public string ST_CD         { get; set; }
public string APPLICABLE            { get; set; }
public string STATUS                { get; set; }
public string TAX_TYPE              { get; set; }
public string LWF_SITE_CODE_REF     { get; set; }
public string FREQUENCY             { get; set; }
public string TOTAL_AMOUNT          { get; set; }
public string EMPLOYEE_TTL_CNTB     { get; set; }
public string EMPLOYEE_MON_CNTB     { get; set; }
public string EMPLOYER_TTL_CNTB     { get; set; }
public string EMPLOYER_MON_CNTB     { get; set; }
        public string DEPOSITE_FREQ { get; set; }
        
public string MIN_WAGES_SLAB        { get; set; }
public string MAX_WAGES_SLAB { get; set; }

     

    }
    public class ZLWF_MASTER_POST_RFCResponse
    {
        public string Vendor_Code { get; set; }
        public string Vendor_Name { get; set; }
        public string PO_Number { get; set; }
        public string Date { get; set; } = String.Empty;
        public string DeliveryDate { get; set; } = String.Empty;
        public string POQty { get; set; } = String.Empty;
    }
}
