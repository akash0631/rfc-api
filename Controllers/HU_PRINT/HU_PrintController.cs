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
    public class HU_SAVE_Response
    {
        public string PO_Number { get; set; }

    }
    public class HU_SAVE
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<HU_SAVE_Response> Data;
      
        public HU_SAVE()
        {
            Data = new List<HU_SAVE_Response>();
        }
    }
    public class HU_SAVE_Request
    {
        public string IM_USER { get; set; }
        public string IM_WERKS { get; set; }
        public string IM_EXIDV { get; set; }
        public string IM_SAP_HU { get; set; }
        
    }
    public class HU_Validate_Response
    {
        public string PO_Number { get; set; }

    }
    public class HU_Validate
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<HU_Validate_Response> Data;

        public HU_Validate()
        {
            Data = new List<HU_Validate_Response>();
        }
    }
    public class HU_Validate_Request
    {
        public string IM_USER { get; set; }
        public string IM_WERKS { get; set; }
        public string IM_EXIDV { get; set; }
        public string IM_SAP_HU { get; set; }

    }

    [RoutePrefix("api/HU_Print")]
    public class HU_PrintController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        [Route("HU_SAVE")]
        public async Task<HttpResponseMessage> HU_SAVE(HU_SAVE_Request request)
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
                    myfun = rfcrep.CreateFunction("ZWM_ACTUAL_HU_SAVE"); //RfcFunctionName

                    myfun.SetValue("IM_USER", request.IM_USER);
                    myfun.SetValue("IM_WERKS", request.IM_WERKS);
                    myfun.SetValue("IM_EXIDV", request.IM_EXIDV);
                    myfun.SetValue("IM_SAP_HU", request.IM_SAP_HU);


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