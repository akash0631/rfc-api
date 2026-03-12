using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office.CustomUI;
using DocumentFormat.OpenXml.Office2016.Excel;
using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_Application_MVC.Models;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using r=Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Controllers.PeperlessPicklist
{
    public class AuthenticateeController: BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] r.AuthenticateRequest request)
        {
            Authenticate Authenticate = new Authenticate();
            return await Task.Run(() =>
            {
                try
                {

                    if (request.Username != "" && request.Password != "" && request.Username != null && request.Password != null)
                    {
                        try
                        {
                            RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                            // Get RfcTable from SAP
                            RfcRepository rfcrep = dest.Repository;
                            IRfcFunction myfun = null;
                            myfun = rfcrep.CreateFunction("ZWM_USER_AUTHORITY_CHECK"); //RfcFunctionName
                            myfun.SetValue("IM_USERID", request.Username); //Import Parameter
                            myfun.SetValue("IM_PASSWORD", request.Password); //Import Parameter
                            myfun.Invoke(dest);


                            //IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                            string E_Werks = myfun.GetValue("EX_WERKS").ToString();
                            //string E_Werks = myfun.GetChar("EX_WERKS").ToString();
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


                                //Distinct(row).ToList();
                                Authenticate.Status = true;
                                Authenticate.Message = "" + SAP_Message + "";
                                Authenticate.StoreId = E_Werks;
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
                //try
                //{

                //    if (request.Username.Trim().ToLower() != "hd22" || request.Password.Trim().ToLower() != "v2@123")
                //    {
                //        return Request.CreateResponse(HttpStatusCode.OK, new
                //        {
                //            Status = false,
                //            Message = "" + "Invalid Credentials" + ""
                //        });
                //    }

                //    return Request.CreateResponse(HttpStatusCode.OK, new
                //    {
                //        Status = true,
                //        Message = "" + "Authenticated Successfully" + "",
                //        StoreId = request.Username
                //        //Data = picnrDataList
                //    });

                //}
                //catch (Exception ex)
                //{
                //    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                //    {
                //        Status = false,
                //        Message = ex.Message
                //    });
                //}
            });
        }
    }
}