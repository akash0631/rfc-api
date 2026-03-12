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
namespace Vendor_Application_MVC.Controllers
{
    public class Update_Routing_StatusController : BaseController
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
                    string baseUrl = Url.Request.RequestUri.GetComponents(
            UriComponents.SchemeAndServer, UriFormat.Unescaped);
                    string Filepath = "";
                    Submit_Routing_StatusRequest request = new Submit_Routing_StatusRequest();
                    if (formData["HHTUSER"] != "" && formData["HHTUSER"] != null &&
                    formData["PO_NO"] != "" && formData["PO_NO"] != null
                    && formData["Design_No"] != "" && formData["Design_No"] != null
                    && formData["Status"] != "" && formData["Status"] != null

                    && formData["Maj_Cat"] != "" && formData["Maj_Cat"] != null
                        && formData["Qty"] != "" && formData["Qty"] != null)
                    {

                        //if (!Request.Content.IsMimeMultipartContent())
                        //{
                        //    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
                        //}



                        IList<HttpContent> files = provider.Files;
                        //if (files.Count > 0)
                        //{

                        request.PO_NO = formData["PO_NO"];
                        request.Design_No = formData["Design_No"];
                        request.Status = formData["Status"];
                        request.Maj_Cat = formData["Maj_Cat"];
                        request.Qty = formData["Qty"];
                        request.HHTUSER = formData["HHTUSER"];
                        request.Article_Number = formData["Article_Number"];
                        request.Remarks = String.IsNullOrEmpty(formData["Remarks"]) ? "" : formData["Remarks"];
                        foreach (var k in files)
                        {

                            string currentyear = DateTime.Parse(DateTime.Now.ToString()).Year.ToString();
                            string currentmonth = DateTime.Now.ToString("MMM");//Parse(DateTime.Now.ToString("MMM")).Month.ToString();
                                                                               //string foldername = DateTime.Now.ToString("dd-MM-yyyy");
                            double Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                            string unique = DateTime.Now.ToString("ddMMyyyyhhssfff");
                            HttpContent file1 = k;
                            //var limit_length = file1.Headers.ContentLength.Value;
                            //var KiloBytes = limit_length / 1000;
                            //var MegaBytes = KiloBytes / 1000;
                            var thisFileName = file1.Headers.ContentDisposition.FileName.Trim('\"');
                            //if (!IsRecognisedImageFile(thisFileName))
                            //{
                            //    PO_Detail.Status = false;
                            //    PO_Detail.Message = "Image not found";
                            //    return Request.CreateResponse(HttpStatusCode.NotFound, PO_Detail);

                            //}
                            //if (MegaBytes > 10)
                            //{
                            //    PO_Detail.Status = false;
                            //    PO_Detail.Message = "Image Size should not be greater than 10 MB. ";
                            //    return Request.CreateResponse(HttpStatusCode.NotFound, PO_Detail);

                            //}


                            string extension = Path.GetExtension(thisFileName);
                            thisFileName = "";

                            string filename = String.Empty;
                            Stream input = await file1.ReadAsStreamAsync();
                            string directoryName = String.Empty;

                            //string tempDocUrl = WebConfigurationManager.AppSettings["DocsUrl"];

                            var path = HttpRuntime.AppDomainAppPath;









                            thisFileName = formData["PO_NO"] + "_" + formData["Status"] + "_" + unique + extension;
                            directoryName = System.IO.Path.Combine(path, "ClientDocument\\" + currentyear + "\\" + currentmonth + "\\" + formData["PO_NO"] + "\\");
                            filename = System.IO.Path.Combine(directoryName, thisFileName);
                            Filepath += baseUrl + "/ClientDocument/" + currentyear + "/" + currentmonth + "/" + formData["PO_NO"] + "/" + thisFileName;
                            Filepath += ",";
                            Directory.CreateDirectory(@directoryName);

                            using (Stream file = File.OpenWrite(filename))
                            {
                                input.CopyTo(file);
                                //close file  
                                file.Close();
                            }

                        }
                        var curr = "ASN_" + DateTime.Now.ToString("ddMMyyyyHHmmssfff");
                        Boolean result = false;// baseUrl.Contains("localhost");
                        if (!result)
                        {
                            RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                            // Get RfcTable from SAP
                            RfcRepository rfcrep = dest.Repository;
                            IRfcFunction myfun = null;
                            myfun = rfcrep.CreateFunction("ZSRM_ROUTING_POST"); //RfcFunctionName
                            IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                            E_Data.SetValue("PO_NO", request.PO_NO);
                            E_Data.SetValue("MAJ_CAT", request.Maj_Cat);
                            E_Data.SetValue("DESIGN_NO", request.Design_No);
                            E_Data.SetValue("QTY", request.Qty);
                            E_Data.SetValue("RTNO", request.Status);
                            E_Data.SetValue("COMP_ART", request.Article_Number);
                            if (request.Status == "200")
                            {
                                E_Data.SetValue("FILEPATH", "NA");
                            }
                            else
                            {
                                E_Data.SetValue("FILEPATH", Filepath.TrimEnd(','));
                            }
                            E_Data.SetValue("REMARKS", request.Remarks);
                            E_Data.SetValue("HHTUSER", request.HHTUSER);
                            if (request.Status == "200")
                            {
                                E_Data.SetValue("ASN_NO", curr);
                            }

                            //myfun.SetValue("IM_USER", request.IM_PO);  //Import Parameter
                            //myfun.SetValue("IM_PO", request.IM_PO);    //Import Parameter

                            myfun.Invoke(dest);
                            IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                            string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                            string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                            if (SAP_TYPE == "E")
                            {
                                PO_Detail.Status = false;
                                PO_Detail.Message = "" + SAP_Message + "";
                                // Generate timestamp in format ddMMYYYYHHMMSSFFF
                                LogHelper.WriteLog("Error : " + PO_Detail.Message + $"[{JsonConvert.SerializeObject(request)}]", "UpdateRoutingStatus", formData["PO_NO"], "Error");
                                return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);

                            }


                            PO_Detail.Status = true;
                            PO_Detail.Message = "" + SAP_Message + "";
                            if (request.Status == "200")
                            {
                                PO_Detail.AsnNo = curr;
                                LogHelper.WriteLog("Asn No. Generated : " + SAP_Message, "UpdateRoutingStatus", formData["PO_NO"], "Success");
                            }
                            //PO_Detail.Message = "Updated Successfully";
                            return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                        }
                        else
                        {
                            PO_Detail.Status = true;
                            PO_Detail.Message = "Submit Successfully";
                            if (request.Status == "200")
                            {
                                PO_Detail.AsnNo = curr;
                            }
                            LogHelper.WriteLog("Unwanted Section : " + PO_Detail.Message, "UpdateRoutingStatus", formData["PO_NO"], "Error");

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
                        LogHelper.WriteLog("Error : " + PO_Detail.Message + $"[{JsonConvert.SerializeObject(request)}]", "UpdateRoutingStatus", formData["PO_NO"], "Error");
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }
                }

                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "" + ex.Message + "";
                    Submit_Routing_StatusRequest request = new Submit_Routing_StatusRequest();

                    //if (!Request.Content.IsMimeMultipartContent())
                    //{
                    //    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
                    //}



                    //IList<HttpContent> files = provider.Files;
                    //if (files.Count > 0)
                    //{

                    request.PO_NO = formData["PO_NO"];
                    request.Design_No = formData["Design_No"];
                    request.Status = formData["Status"];
                    request.Maj_Cat = formData["Maj_Cat"];
                    request.Qty = formData["Qty"];
                    request.HHTUSER = formData["HHTUSER"];
                    request.Article_Number = formData["Article_Number"];
                    LogHelper.WriteLog("Error : " + PO_Detail.Message + $"[{JsonConvert.SerializeObject(request)}]", "UpdateRoutingStatus", formData["PO_NO"], "Error");

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