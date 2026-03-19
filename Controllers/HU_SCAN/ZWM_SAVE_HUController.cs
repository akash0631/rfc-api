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
    public class ZWM_SAVE_HUController : BaseController
    {
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post(ZWM_SAVE_HUREQUEST request)
        {
            Submit_Routing_Status PO_Detail = new Submit_Routing_Status();
            return await Task.Run(() =>
            {
                try
                {
                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                    // Get RfcTable from SAP 
                    RfcRepository rfcrep = dest.Repository;
                    IRfcFunction myfun = null;
                    myfun = rfcrep.CreateFunction("ZWM_SAVE_HU"); //RfcFunctionName
                    myfun.SetValue("IM_USER", request.IM_USER);
                    myfun.SetValue("IM_PLANT", request.IM_PLANT);
                    myfun.SetValue("IM_HU", request.IM_HU);
                    IRfcTable IrfTable = myfun.GetTable("IM_ARTICLES");
                    //IRfcStructure IrfTable = E_Data.Metadata.LineType.CreateStructure();
                    foreach (var req in request.IM_ARTICLES) // assuming 'requestList' is your list of request objects
                    {
                        IrfTable.Append();
                        IrfTable.SetValue("MATNR", req.MATNR);
                        IrfTable.SetValue("HU_QTY", req.HU_QTY);
                        IrfTable.SetValue("SCAN_QTY", req.SCAN_QTY);
                        IrfTable.SetValue("DIFF_QTY", req.DIFF_QTY);
                        
                        

                        
                    }
                    myfun.Invoke(dest);

                    IRfcStructure E_RETURN = myfun.GetStructure("ET_ERROR");
                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                    if (SAP_TYPE == "E")
                    {
                        PO_Detail.Status = false;
                        PO_Detail.Message = "" + SAP_Message + "";
                       // LogHelper.WriteLog(SAP_Message, "ASN_APPROVED_POST");
                        return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                    }


                    PO_Detail.Status = true;
                    PO_Detail.Message = "" + SAP_Message + "";
                   // LogHelper.WriteLog(SAP_Message, "ASN_APPROVED_POST");
                    //PO_Detail.Message = "Updated Successfully";
                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                }
                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "" + ex.Message + "";
                    //LogHelper.WriteLog(PO_Detail.Message, "ASN_APPROVED_POST");
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

        
    }

    public class ZWM_SAVE_HUREQUEST
    {
        public string IM_USER { get; set; } = String.Empty;
        public string IM_PLANT { get; set; } = String.Empty;
        public string IM_HU { get; set; } = String.Empty;
        public List<ZWM_SAVE_HU_article> IM_ARTICLES { get; set; } 
        
    }
    public class ZWM_SAVE_HU_article
    {
        public string MATNR { get; set; } = String.Empty;
        public string SCAN_QTY { get; set; } = String.Empty;
        public string HU_QTY { get; set; } = String.Empty;
        public string DIFF_QTY { get; set; } = String.Empty;
    }
}