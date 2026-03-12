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
    public class RRUpdate_Routing_StatusController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post()
        {
            Submit_Routing_Status PO_Detail = new Submit_Routing_Status();
            
                try
                {
                    var provider = await Request.Content.ReadAsMultipartAsync<InMemoryMultipartFormDataStreamProvider>(new InMemoryMultipartFormDataStreamProvider());
                    NameValueCollection formData = provider.FormData;
                    string baseUrl = Url.Request.RequestUri.GetComponents(
            UriComponents.SchemeAndServer, UriFormat.Unescaped);
                    if (formData["PO_NO"] != "" && formData["PO_NO"] != null
                    && formData["ARTICLE"] != "" && formData["ARTICLE"] != null
                        && formData["STATUS"] != "" && formData["STATUS"] != null
                        //&& formData["PoId"] != "" && formData["PoId"] != null
                        )
                    {
                        Submit_RR_Routing_StatusRequest request = new Submit_RR_Routing_StatusRequest();
                        //if (!Request.Content.IsMimeMultipartContent())
                        //{
                        //    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
                        //}



                        IList<HttpContent> files = provider.Files;
                        //if (files.Count > 0)
                        //{

                        request.PO_NO = formData["PO_NO"];
                        request.Status = formData["STATUS"];
                        request.Article = formData["ARTICLE"];
                        request.PoId = formData["PoId"];

                        Boolean result = false;// baseUrl.Contains("localhost");
                        if (!result)
                        {
                            RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                            // Get RfcTable from SAP
                            RfcRepository rfcrep = dest.Repository;
                            IRfcFunction myfun = null;
                            myfun = rfcrep.CreateFunction("ZSRM_ROUTING_POST_NEW"); //RfcFunctionName
                                                                                    //IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                            myfun.SetValue("IM_PO_NO", request.PO_NO);
                            myfun.SetValue("IM_GEN_ART", request.Article);
                            myfun.SetValue("IM_RTNO", request.Status);
                            myfun.SetValue("IM_HHTUSER", "300");
                            //var asnNo = "ASN_" + DateTime.Now.ToString("ddMMyyyyHHmmssfff"); 
                            var asnNo = "ASN_" + request.PoId;
                            if (request.Status.ToString() == "200")
                            {
                                myfun.SetValue("IM_ASN", asnNo);
                            }
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
                            if (request.Status.ToString() == "200")
                            {
                                PO_Detail.AsnNo = asnNo;
                            }

                            PO_Detail.Status = true;
                            PO_Detail.Message = "" + SAP_Message + "";

                            //PO_Detail.Message = "Updated Successfully";
                            return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                        }
                        else
                        {
                            PO_Detail.Status = true;
                            PO_Detail.Message = "Submit Successfully";

                            //PO_Detail.Message = "Updated Successfully";
                            return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                        }
                        //}
                        //else
                        //{
                        //    PO_Detail.Status = false;
                        //    PO_Detail.Message = "Image not found";
                        //    return Request.CreateResponse(HttpStatusCode.NotFound, PO_Detail);
                        //}
                    }
                    else
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = "All request field is Mandatory.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }
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