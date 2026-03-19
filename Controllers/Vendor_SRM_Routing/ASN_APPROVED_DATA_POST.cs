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
namespace Vendor_Application_MVC.Controllers
{
    public class ASN_APPROVED_DATA_POSTController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post(List<ASN_APPROVED_DATA_POST_REQUEST> request)
        {
            Submit_Routing_Status PO_Detail = new Submit_Routing_Status();
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP 
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZSRM_ASN_APPROVED_DATA_POST"); //RfcFunctionName
                    IRfcTable IrfTable = myfun.GetTable("IT_DATA");
                    //IRfcStructure IrfTable = E_Data.Metadata.LineType.CreateStructure();
                    foreach (var req in request) // assuming 'requestList' is your list of request objects
                    {
                        IrfTable.Append();
                        IrfTable.SetValue("EBELN", req.PO_NUMBER);
                        IrfTable.SetValue("SATNR", req.SATNR);
                        IrfTable.SetValue("MATNR", req.ARTICLE_NUMBER);
                        IrfTable.SetValue("PO_QTY", req.PO_QTY);
                        IrfTable.SetValue("APPROVED_QTY", req.APPROVED_QTY);
                        IrfTable.SetValue("APPROVED_ON", req.APPROVED_ON);
                        IrfTable.SetValue("ASN_NUMBER", req.ASN_NUMBER);
                        IrfTable.SetValue("BILL_NUMBER", req.BILL_NUMBER);

                        
                    }
                    myfun.Invoke(dest);

                    IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");
                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                    if (SAP_TYPE == "E")
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = "" + SAP_Message + "";
                        LogHelper.WriteLog(SAP_Message, "ASN_APPROVED_POST");
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }


                    PO_Detail.Status = true;
                    PO_Detail.Message = "" + SAP_Message + "";
                    LogHelper.WriteLog(SAP_Message, "ASN_APPROVED_POST");
                    //PO_Detail.Message = "Updated Successfully";
                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                }
                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "" + ex.Message + "";
                    LogHelper.WriteLog(PO_Detail.Message, "ASN_APPROVED_POST");
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

    public class ASN_APPROVED_DATA_POST_REQUEST
    {
        public string PO_NUMBER { get; set; } = String.Empty;
        public string SATNR { get; set; } = String.Empty;
        public string ARTICLE_NUMBER { get; set; } = String.Empty;
        public string PO_QTY { get; set; } = String.Empty;
        public string APPROVED_QTY { get; set; } = String.Empty;
        public string APPROVED_ON { get; set; } = String.Empty;
        public string ASN_NUMBER { get; set; } = String.Empty;
        public string BILL_NUMBER { get; set; } = String.Empty;
    }
}