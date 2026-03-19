using SAP.Middleware.Connector;
using System;
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
    public class ZPO_MODIFICATIONController : BaseController
    {
        // [HttpPost]
        // public void UploadFile()
        // {
        //     var file = HttpContext.Current.Request.Files.Count > 0 ?
        //HttpContext.Current.Request.Files[0] : null;
        // }

        public async Task<HttpResponseMessage> POST([FromBody] ZPO_MODIFICATIONRequest request)
        {
            ZPO_MODIFICATION Authenticate = new ZPO_MODIFICATION();

            try
            {


                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZPO_MODIFICATION"); //RfcFunctionName


                    myfun.SetValue("IM_PO_NO", request.IM_PO_NO); //Import Parameter
                    myfun.SetValue("IM_PO_DEL_DATE", request.IM_PO_DEL_DATE); //Import Parameter
                    myfun.SetValue("IM_DEL_CHG_DATE_LOW", request.IM_DEL_CHG_DATE_LOW); //Import Parameter
                    myfun.SetValue("IM_DEL_CHG_DATE_HIGH", request.IM_DEL_CHG_DATE_HIGH); //Import Parameter
                    myfun.Invoke(dest);


                    IRfcTable IrfTable = myfun.GetTable("ET_PO_OUTPUT");
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

                        for (int i = 0; i < IrfTable.RowCount; ++i)
                        {
                            ZPO_MODIFICATIONResponse authenticateResponse = new ZPO_MODIFICATIONResponse();
                           

                            authenticateResponse.REASON = IrfTable[i].GetString("REASON");
                            authenticateResponse.DELAYED_BY = IrfTable[i].GetString("DELAYED_BY");
                            authenticateResponse.DEL_EXT_DATE = IrfTable[i].GetString("DEL_EXT_DATE");
                            authenticateResponse.CURRENT_DEL_DATE = IrfTable[i].GetString("CURRENT_DEL_DATE");
                            authenticateResponse.CHNG_NO = IrfTable[i].GetString("CHNG_NO");
                            authenticateResponse.ORIGNAL_DEL_DATE = IrfTable[i].GetString("ORIGNAL_DEL_DATE");
                            authenticateResponse.EBELN = IrfTable[i].GetString("EBELN");

                            Authenticate.Data.Add(authenticateResponse);
                        }
                        Authenticate.Data = Authenticate.Data.ToList();
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
            catch (Exception ex)
            {
                Authenticate.Status = false;
                Authenticate.Message = "" + ex.Message + "";
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
            }


        }

    }

    public class ZPO_MODIFICATION
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<ZPO_MODIFICATIONResponse> Data;
        public ZPO_MODIFICATION()
        {
            Data = new List<ZPO_MODIFICATIONResponse>();
        }
    }
    public class ZPO_MODIFICATIONRequest
    {
        public string IM_PO_NO { get; set; }
        public string IM_PO_DEL_DATE { get; set; }
        public string IM_DEL_CHG_DATE_LOW { get; set; }
        public string IM_DEL_CHG_DATE_HIGH { get; set; }



    }
    public class ZPO_MODIFICATIONResponse
    {
        public string EBELN { get; set; }
        public string ORIGNAL_DEL_DATE { get; set; }
        public string CHNG_NO { get; set; }
        public string CURRENT_DEL_DATE { get; set; }
        public string DEL_EXT_DATE { get; set; }
        public string DELAYED_BY { get; set; }
        public string REASON { get; set; }


    }

}
