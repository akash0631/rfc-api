

using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using Vendor_Application_MVC.Controllers;

namespace VendorSRM_Application.Controllers.API
{
    public class HU_AutoUpdateController : BaseController
    {
        [System.Web.Mvc.HttpPost]
        public async Task<HttpResponseMessage> POST([FromBody] FMSAutoupdateRequest request)
        {
            
            FMSAutoUpdate Autoupdate = new FMSAutoUpdate();
            return await Task.Run(() =>
            {
                try
                {

                    if (request.Version_Code != "" && request.Version_Code != null
                        && request.Version_Name != "" && request.Version_Name != null
                        && request.Drive_Link != "" && request.Drive_Link != null)
                    {
                        string createText = request.Version_Code + "," + request.Version_Name + "," + request.Drive_Link;

                        File.WriteAllText("D:\\V2 Published\\FMS_PUTWAY\\HU_AutoUpdate.txt", createText);

                        FMSAutoupdateResponse AutoupdateResponse = new FMSAutoupdateResponse();
                        AutoupdateResponse.Version_Code = request.Version_Code;
                        AutoupdateResponse.Version_Name = request.Version_Name;
                        AutoupdateResponse.Drive_Link = request.Drive_Link;


                        Autoupdate.Data.Add(AutoupdateResponse);
                        Autoupdate.Status = true;
                        Autoupdate.Message = "";
                        return Request.CreateResponse(HttpStatusCode.OK, Autoupdate);


                    }
                    else
                    {
                        Autoupdate.Status = false;
                        Autoupdate.Message = "All Field is Mandatory.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, Autoupdate);
                    }
                }
                catch (Exception ex)
                {
                    Autoupdate.Status = false;
                    Autoupdate.Message = "" + ex.Message + "";
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, Autoupdate);
                }
            });


        }

        [System.Web.Mvc.HttpGet]
        public async Task<HttpResponseMessage> GET()
        {
            FMSAutoUpdate Autoupdate = new FMSAutoUpdate();

            try
            {


                //string createText = "Hello and Welcome" + Environment.NewLine;
                //File.WriteAllText(path, createText);
                var readText = File.ReadAllText("D:\\V2 Published\\FMS_PUTWAY\\HU_AutoUpdate.txt").Split(',');
                    FMSAutoupdateResponse AutoupdateResponse = new FMSAutoupdateResponse();
                AutoupdateResponse.Version_Code = readText[0].ToString();
                    AutoupdateResponse.Version_Name = readText[1].ToString();
                AutoupdateResponse.Drive_Link = readText[2].ToString();


                Autoupdate.Data.Add(AutoupdateResponse);
                    Autoupdate.Status = true;
                    Autoupdate.Message = "";
                    return Request.CreateResponse(HttpStatusCode.OK, Autoupdate);


               
            }
            catch (Exception ex)
            {
                Autoupdate.Status = false;
                Autoupdate.Message = "" + ex.Message + "";
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Autoupdate);
            }


        }
    }
}