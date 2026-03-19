using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Models;
using System.Threading.Tasks;
using System.Configuration;
using Newtonsoft.Json.Linq;
using DocumentFormat.OpenXml.Office2016.Excel;
using System.Web.Http.Cors;
using System.IO;
using System.Threading.Tasks;
namespace Vendor_Application_MVC.Controllers
{
    public class HU_Identification_Response
    {
        
            public string SAP_HU { get; set; }
        public string EXIDV { get; set; }
        public string ST_CD { get; set; }
        public string ST_NAME { get; set; }

    }
    public class HU_Identification
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<HU_Identification_Response> Data;
      
        public HU_Identification()
        {
            Data = new List<HU_Identification_Response>();
        }
    }
    public class HU_Identification_Request
    {
        public string IM_HU{ get; set; }
       
        
    }
   

    [RoutePrefix("api/HU_Identification")]
    public class HU_IdentificationController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        [Route("HU_Identification")]
        public async Task<HttpResponseMessage> HU_Identification(HU_Identification_Request request)
        {
            HU_Identification PO_Detail = new HU_Identification();
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZWM_HU_STORE_RFC"); //RfcFunctionName

                    myfun.SetValue("IM_HU", request.IM_HU);
                   


                    myfun.Invoke(dest);
                    IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
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
                        IRfcTable E_RESPONSE = myfun.GetTable("EX_DATA");

                        List<HU_Identification_Response> list = new List<HU_Identification_Response>();


                        HU_Identification_Response obj = new HU_Identification_Response();
                        //var obj = new NSOSiteListResponse();
                        obj.EXIDV = E_RESPONSE.GetString("EXIDV");
                        //obj.SAP_HU = E_RESPONSE.GetString("SAP_HU");
                        obj.ST_CD = E_RESPONSE.GetString("ST_CD");
                        obj.ST_NAME = E_RESPONSE.GetString("ST_NAME");


                        list.Add(obj);


                        PO_Detail.Status = true;
                        PO_Detail.Message = "" + SAP_Message + "";
                        PO_Detail.Data = list;




                        //PO_Detail.Message = "Updated Successfully";
                        return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);

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

        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> HU_Validate(HU_Validate_Request request)
        {
            HU_SAVE PO_Detail = new HU_SAVE();
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZWM_EXTERNAL_HU_VALIDATE"); //RfcFunctionName

                    myfun.SetValue("IM_USER", request.IM_USER);
                    myfun.SetValue("IM_WERKS", request.IM_WERKS);
                    myfun.SetValue("IM_EXIDV", request.IM_EXIDV);



                    myfun.Invoke(dest);
                    IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                    if (SAP_TYPE == "E")
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = "" + SAP_Message + "";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }


                    PO_Detail.Status = true;
                    PO_Detail.Message = "" + SAP_Message + "";

                    //PO_Detail.Message = "Updated Successfully";
                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);


                }

                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "" + ex.Message + "";
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);
                }
            });


        }

        public static bool IsRecognisedImageFile(string fileName)
        {
            string targetExtension = System.IO.Path.GetExtension(fileName);
            if (String.IsNullOrEmpty(targetExtension))
            {
                return false;
            }

            var recognisedImageExtensions = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().SelectMany(codec => codec.FilenameExtension.ToLowerInvariant().Split(';'));

            targetExtension = "*" + targetExtension.ToLowerInvariant();
            return recognisedImageExtensions.Contains(targetExtension);
        }


        public async Task WriteToFileAsync(Stream input, string filename)
        {
            // Open the file asynchronously with the FileStream constructor
            using (FileStream fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                // Copy the input stream to the file stream asynchronously
                await input.CopyToAsync(fileStream);
            }
        }
    }
}