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
    public class NSOConfigRoutingController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post(NSOConfigRoutingRequest request)
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
                    myfun = rfcrep.CreateFunction("ZSRM_NSO_CONF_ROUTING"); //RfcFunctionName
                                                                            //IRfcStructure E_Data = myfun.GetStructure("EX_DATA");

                    myfun.SetValue("IM_SITE_CODE", request.SiteCode);
                    //E_Data.SetValue("PO_NO", request.PO_NO);
                    //E_Data.SetValue("MAJ_CAT", request.Maj_Cat);
                    //E_Data.SetValue("DESIGN_NO", request.Design_No);
                    //E_Data.SetValue("QTY", request.Qty);
                    //E_Data.SetValue("RTNO", request.Status);

                    myfun.Invoke(dest);
                    IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                    IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                    if (SAP_TYPE == "E")
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = "" + SAP_Message + "";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }

                    var list = new List<NSOConfigRoutingResponse>();

                    for (int i = 0; i < IrfTable.RowCount; ++i)
                    {
                        var obj = new NSOConfigRoutingResponse();
                        obj.MANDT = IrfTable[i].GetString("MANDT");
                        obj.SRNO = IrfTable[i].GetString("SRNO");
                        obj.SPRAS = IrfTable[i].GetString("SPRAS");
                        obj.TEXT = IrfTable[i].GetString("TEXT");
                        list.Add(obj);
                    }
                    PO_Detail.Status = true;
                    PO_Detail.Message = "" + SAP_Message + "";
                    return Request.CreateResponse(HttpStatusCode.OK, list);
                    //return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                }
                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "" + ex.Message + "";
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);
                    //return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);
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

    public class NSOConfigRoutingRequest
    {
        public string SiteCode { get; set; } = String.Empty; 
    }

    public class NSOConfigRoutingResponse {
        public string SITE_CODE { get; set; } = String.Empty;
        public string MANDT { get; set; } = String.Empty;
        public string SRNO { get; set; } = String.Empty;
        public string SPRAS { get; set; } = String.Empty;
        public string TEXT { get; set; } = String.Empty;
    }
}