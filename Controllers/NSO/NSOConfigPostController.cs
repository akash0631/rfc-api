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
    public class NSOConfigPostController : BaseController
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

                if (
                    //string.IsNullOrEmpty(formData["IM_CSTATUS"]) ||
                    string.IsNullOrEmpty(formData["IM_SRNO"]) ||
                    string.IsNullOrEmpty(formData["IM_SRNO_Desc"]) ||
                  
                    string.IsNullOrEmpty(formData["IM_ACT_ST_DATE"]) ||
                    string.IsNullOrEmpty(formData["IM_ACT_END_DATE"]) ||
                    string.IsNullOrEmpty(formData["IM_START_AT_TIME"]) ||
                    string.IsNullOrEmpty(formData["IM_END_AT_TIME"]) ||
                    string.IsNullOrEmpty(formData["IM_PROCESS_CONFIRM"]) ||
                    string.IsNullOrEmpty(formData["IM_REMARKS"]) ||
               
                    string.IsNullOrEmpty(formData["IM_ROUTING_NO"])
                    ||
                    string.IsNullOrEmpty(formData["IM_Rout_Desc"])
                    )
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "Missing required fields in form data.";
                    return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                }
                // Extract form data
                NSOConfigPostRequest request = new NSOConfigPostRequest
                {
                    IM_CSTATUS = "",
                    IM_SRNO = formData["IM_SRNO"],
                    IM_BUDGET_ST_DATE = formData["IM_BUDGET_ST_DATE"],
                    IM_BUDGET_END_DATE = formData["IM_BUDGET_END_DATE"],
                    IM_ACT_ST_DATE = formData["IM_ACT_ST_DATE"],
                    IM_ACT_END_DATE = formData["IM_ACT_END_DATE"],
                    IM_START_AT_TIME = formData["IM_START_AT_TIME"],
                    IM_END_AT_TIME = formData["IM_END_AT_TIME"],
                    IM_REMARKS = formData["IM_REMARKS"],
                    IM_REMARKS1 = formData["IM_REMARKS1"],
                    IM_REMARKS2 = formData["IM_REMARKS2"],
                    IM_ROUTING_NO = formData["IM_ROUTING_NO"],
                    IM_PROCESS_CONFIRM = formData["IM_PROCESS_CONFIRM"],
                    IM_SRNO_Desc = formData["IM_SRNO_Desc"],
                    IM_Rout_Desc = formData["IM_Rout_Desc"]
                };
                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZSRM_NSO_CONF_POST");

                //myfun.SetValue("IM_CSTATUS", "");
                myfun.SetValue("IM_SRNO", request.IM_SRNO);
                myfun.SetValue("IM_BUDGTE_ST_DATE", request.IM_BUDGET_ST_DATE);
                myfun.SetValue("IM_BUDGTE_END_DATE", request.IM_BUDGET_END_DATE);
                myfun.SetValue("IM_ACT_START_DATE", request.IM_ACT_ST_DATE);
                myfun.SetValue("IM_ACT_END_DATE", request.IM_ACT_END_DATE);
                myfun.SetValue("IM_START_AT_TIME", request.IM_START_AT_TIME);
                myfun.SetValue("IM_END_AT_TIME", request.IM_END_AT_TIME);
                myfun.SetValue("IM_REMARKS", request.IM_REMARKS);
                myfun.SetValue("IM_REMARKS1", request.IM_REMARKS1);
                myfun.SetValue("IM_REMARKS2", request.IM_REMARKS2);
                myfun.SetValue("IM_ROUTING_NO", request.IM_ROUTING_NO);
                myfun.SetValue("IM_PROCESS_CONFIRM", request.IM_PROCESS_CONFIRM);

                //IM_HYPERLINK
                IList<HttpContent> files = provider.Files;
                var midPath = Path.Combine(
                    "nsouploads",
                    request.IM_SRNO_Desc, request.IM_Rout_Desc, $"{DateTime.Now:dd-MM-yyyy}" // Example based on request
                       
                    );
                    myfun.SetValue("IM_HYPERLINK", midPath);

                    // Assuming 'files' is of type IEnumerable<MultipartFileData>
                    var fileNamesList = files != null && files.Count > 0
                    ? files.Select((file, index) =>
                    {
                        var fileName = $"{request.IM_SRNO}_{DateTime.Now:ddMMyyyyHHmmssffff}_{index}{Path.GetExtension(file.Headers.ContentDisposition.FileName.Trim('\"'))}";
                        var filePath = Path.Combine(midPath, fileName);
                        return new
                        {
                            FilePath = filePath,
                            FileName = fileName
                        };
                    }).ToList()
                    : null;

                    List<string> savedFiles = new List<string>();




                    myfun.Invoke(dest);

                    IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();

                    if (SAP_TYPE == "E")
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = SAP_Message;
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }
                    if (files != null && files.Count > 0)
                    {
                        int index = 0;
                        foreach (var file in files)
                        {
                            // Ensure file is valid (has content)
                            if (file != null && file.Headers.ContentLength > 0)
                            {
                                // Step 1: Define the folder path where you want to save the file
                                var folderPath = Path.Combine("D:\\V2 Published\\NSO", midPath);

                                // Step 2: Ensure the directory exists
                                if (!Directory.Exists(folderPath))
                                {
                                    Directory.CreateDirectory(folderPath);
                                }

                                // Step 3: Get the unique filename from fileNamesList
                                var uniqueFileName = fileNamesList[index].FileName;

                                // Step 4: Get the complete file path
                                var filePath = Path.Combine(folderPath, uniqueFileName);

                                // Step 5: Save the file
                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                // Step 6: Add the saved file path to the list (or use the URL for response)
                                savedFiles.Add(filePath); // Save the file path or URL if needed
                            }
                            index++;
                        }
                    }

                    PO_Detail.Status = true;
                    PO_Detail.Message = SAP_Message;
                    //PO_Detail.UploadedFiles = savedFiles; // Include file details in response

                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                }
                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = ex.Message;
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

    public class NSOConfigPostRequest
    {
        public string IM_CSTATUS { get; set; } = String.Empty;
        public string IM_SRNO { get; set; } = String.Empty;
        public string IM_BUDGET_ST_DATE { get; set; } = String.Empty;
        public string IM_BUDGET_END_DATE { get; set; } = String.Empty;
        public string IM_ACT_ST_DATE { get; set; } = String.Empty;
        public string IM_ACT_END_DATE { get; set; } = String.Empty;
        public string IM_START_AT_TIME { get; set; } = String.Empty;
        public string IM_END_AT_TIME { get; set; } = String.Empty;
        public string IM_REMARKS { get; set; } = String.Empty;
        public string IM_REMARKS1 { get; set; } = String.Empty;
        public string IM_REMARKS2 { get; set; } = String.Empty;
        public string IM_ROUTING_NO { get; set; } = String.Empty;
        public string IM_PROCESS_CONFIRM { get; set; } = String.Empty;
        public string IM_SRNO_Desc { get; set; } = String.Empty;
        public string IM_Rout_Desc { get; set; } = String.Empty;
        

    }
}