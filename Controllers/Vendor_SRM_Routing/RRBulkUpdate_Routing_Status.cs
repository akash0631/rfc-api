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
using OfficeOpenXml;
namespace Vendor_Application_MVC.Controllers
{
    public class RRBulkUpdate_Routing_StatusController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post()
        {
            Submit_Routing_Status PO_Detail = new Submit_Routing_Status();
            
                try
                {
                    var provider = await Request.Content.ReadAsMultipartAsync<InMemoryMultipartFormDataStreamProvider>(new InMemoryMultipartFormDataStreamProvider());
                    IList<HttpContent> files = provider.Files;

                    if (files.Count == 0)
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = "Excel file not found.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }

                    HttpContent file = files[0];
                    string fileName = file.Headers.ContentDisposition.FileName.Trim('\"');
                    byte[] fileBytes = await file.ReadAsByteArrayAsync();
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(new MemoryStream(fileBytes)))
                    {
                        var worksheet = package.Workbook.Worksheets[0]; // Get the first worksheet
                        int rowCount = worksheet.Dimension.Rows;       // Total rows in the sheet

                        for (int row = 2; row <= rowCount; row++) // Start from row 2 (assuming row 1 is the header)
                        {
                            string poNo = worksheet.Cells[row, 1].Text;   // PO_NO
                            string article = worksheet.Cells[row, 2].Text; // ARTICLE
                            string status = worksheet.Cells[row, 3].Text;  // STATUS

                            if (!string.IsNullOrWhiteSpace(poNo) && !string.IsNullOrWhiteSpace(article) && !string.IsNullOrWhiteSpace(status))
                            {
                                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                                RfcRepository rfcrep = dest.Repository;
                                IRfcFunction myfun = rfcrep.CreateFunction("ZSRM_ROUTING_POST_NEW");

                                myfun.SetValue("IM_PO_NO", poNo);
                                myfun.SetValue("IM_GEN_ART", article);
                                myfun.SetValue("IM_RTNO", status);
                                myfun.SetValue("IM_HHTUSER", "300");

                                myfun.Invoke(dest);

                                IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                                string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                                string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();

                                if (SAP_TYPE == "E")
                                {
                                    PO_Detail.Status = false;
                                    PO_Detail.Message = $"Error updating PO: {poNo}, Article: {article}, Message: {SAP_Message} in row no. {row - 1}";
                                    return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                                }
                            }
                            else
                            {
                                PO_Detail.Status = false;
                                PO_Detail.Message = $"Missing mandatory fields in row {row}.";
                                return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                            }
                        }
                    }

                    PO_Detail.Status = true;
                    PO_Detail.Message = "All rows processed successfully.";
                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                }
                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = $"An error occurred: {ex.Message}";
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