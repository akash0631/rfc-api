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
using Vendor_SRM_Routing_Application.Utils.Logger;
using Newtonsoft.Json;
using System.Linq.Expressions;
namespace Vendor_Application_MVC.Controllers
{
    public class PPT_POSTController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post()
        {
                var provider = await Request.Content.ReadAsMultipartAsync<InMemoryMultipartFormDataStreamProvider>(new InMemoryMultipartFormDataStreamProvider());
                NameValueCollection formData = provider.FormData;
            Submit_Routing_Status PO_Detail = new Submit_Routing_Status();

            try
            {
                Submit_Routing_StatusRequest request = new Submit_Routing_StatusRequest();
                if (formData["PPT_NO"] != "" && formData["PPT_NO"] != null &&
                formData["RTNO"] != "" && formData["RTNO"] != null)

                {

                    //if (!Request.Content.IsMimeMultipartContent())
                    //{
                    //    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
                    //}



                    IList<HttpContent> files = provider.Files;
                    //if (files.Count > 0)
                    //{

                     string PPT_NO = formData["PPT_NO"];
                    string RTNO = formData["RTNO"];

                    string START_AT_TIME = formData["START_AT_TIME"];
                    string END_AT_TIME = formData["END_AT_TIME"];
                    string remarks = formData["Remarks"];
                    string numberofpassdesign = formData["numberofpassdesign"];


                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZRFC_PPT_CONF_POST"); //RfcFunctionName
                    IRfcStructure E_Data = myfun.GetStructure("IM_DATA");
                    E_Data.SetValue("PPT_NO", PPT_NO);
                    E_Data.SetValue("RTNO", RTNO);
                    E_Data.SetValue("START_AT_TIME", START_AT_TIME);
                    E_Data.SetValue("END_AT_TIME", END_AT_TIME);
                    E_Data.SetValue("REMARKS", remarks);
                    E_Data.SetValue("PPT_ACCT", numberofpassdesign);
                    //E_Data.SetValue("RTNO", request.Status);
                    //E_Data.SetValue("COMP_ART", request.Article_Number);


                    //myfun.SetValue("PPT_NO", request.PO_NO);  //Import Parameter
                    //myfun.SetValue("RTNO", request.Design_No);    //Import Parameter

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


                    PO_Detail.Status = true;
                    PO_Detail.Message = "" + SAP_Message + "";

                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                }

                PO_Detail.Status = false;
                PO_Detail.Message = "PPT_No and RTNO is mandatory";

                return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
            }
            catch (Exception ex)
            {
                PO_Detail.Status = false;
                PO_Detail.Message = "" + ex.Message + "";

                return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);
            }
              

           

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