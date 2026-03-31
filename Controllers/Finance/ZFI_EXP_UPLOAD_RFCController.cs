using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_EXP_UPLOAD_RFC")]
        public async Task<IHttpActionResult> ProcessFinanceExpenseUpload(ZFI_EXP_UPLOAD_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { Status = "E", Message = "Request cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                // Set input parameters
                IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                if (request.IM_INPUT != null)
                {
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        imInputTable.Append();
                        imInputTable.SetValue("COMPANY_CODE", inputItem.COMPANY_CODE ?? "");
                        imInputTable.SetValue("INVOICE_DATE", inputItem.INVOICE_DATE ?? "");
                        imInputTable.SetValue("POSTING_DATE", inputItem.POSTING_DATE ?? "");
                        imInputTable.SetValue("VENDOR_CODE", inputItem.VENDOR_CODE ?? "");
                        imInputTable.SetValue("HEADER_TEXT", inputItem.HEADER_TEXT ?? "");
                        imInputTable.SetValue("WH_TAX_CODE", inputItem.WH_TAX_CODE ?? "");
                        imInputTable.SetValue("REFRENCE_NUMBER", inputItem.REFRENCE_NUMBER ?? "");
                        imInputTable.SetValue("VENDOR_LINE_TEXT", inputItem.VENDOR_LINE_TEXT ?? "");
                        imInputTable.SetValue("GL_CODE", inputItem.GL_CODE ?? "");
                        imInputTable.SetValue("AMOUNT", inputItem.AMOUNT ?? "");
                        imInputTable.SetValue("TAX_CODE", inputItem.TAX_CODE ?? "");
                        imInputTable.SetValue("COST_CENTER", inputItem.COST_CENTER ?? "");
                        imInputTable.SetValue("BUSINESS_AREA", inputItem.BUSINESS_AREA ?? "");
                        imInputTable.SetValue("PROFIT_CENTER", inputItem.PROFIT_CENTER ?? "");
                        imInputTable.SetValue("ASSIGNMENT_NO", inputItem.ASSIGNMENT_NO ?? "");
                        imInputTable.SetValue("GL_LINE_TEXT", inputItem.GL_LINE_TEXT ?? "");
                    }
                }

                myfun.SetValue("COMPANY_CODE", request.COMPANY_CODE ?? "");
                myfun.SetValue("INVOICE_DATE", request.INVOICE_DATE ?? "");
                myfun.SetValue("POSTING_DATE", request.POSTING_DATE ?? "");
                myfun.SetValue("VENDOR_CODE", request.VENDOR_CODE ?? "");
                myfun.SetValue("HEADER_TEXT", request.HEADER_TEXT ?? "");
                myfun.SetValue("WH_TAX_CODE", request.WH_TAX_CODE ?? "");
                myfun.SetValue("REFRENCE_NUMBER", request.REFRENCE_NUMBER ?? "");
                myfun.SetValue("VENDOR_LINE_TEXT", request.VENDOR_LINE_TEXT ?? "");
                myfun.SetValue("GL_CODE", request.GL_CODE ?? "");
                myfun.SetValue("AMOUNT", request.AMOUNT ?? "");
                myfun.SetValue("TAX_CODE", request.TAX_CODE ?? "");
                myfun.SetValue("COST_CENTER", request.COST_CENTER ?? "");
                myfun.SetValue("BUSINESS_AREA", request.BUSINESS_AREA ?? "");
                myfun.SetValue("PROFIT_CENTER", request.PROFIT_CENTER ?? "");
                myfun.SetValue("ASSIGNMENT_NO", request.ASSIGNMENT_NO ?? "");
                myfun.SetValue("GL_LINE_TEXT", request.GL_LINE_TEXT ?? "");

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string status = EX_RETURN.GetValue("TYPE")?.ToString() ?? "";
                string message = EX_RETURN.GetValue("MESSAGE")?.ToString() ?? "";

                if (status == "E")
                {
                    return Json(new { Status = "E", Message = message });
                }

                return Json(new { Status = status, Message = message });
            }
            catch (RfcAbapException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZFI_EXP_UPLOAD_Request
    {
        public List<ZFI_EXP_INPUT_Item> IM_INPUT { get; set; }
        public string COMPANY_CODE { get; set; }
        public string INVOICE_DATE { get; set; }
        public string POSTING_DATE { get; set; }
        public string VENDOR_CODE { get; set; }
        public string HEADER_TEXT { get; set; }
        public string WH_TAX_CODE { get; set; }
        public string REFRENCE_NUMBER { get; set; }
        public string VENDOR_LINE_TEXT { get; set; }
        public string GL_CODE { get; set; }
        public string AMOUNT { get; set; }
        public string TAX_CODE { get; set; }
        public string COST_CENTER { get; set; }
        public string BUSINESS_AREA { get; set; }
        public string PROFIT_CENTER { get; set; }
        public string ASSIGNMENT_NO { get; set; }
        public string GL_LINE_TEXT { get; set; }
    }

    public class ZFI_EXP_INPUT_Item
    {
        public string COMPANY_CODE { get; set; }
        public string INVOICE_DATE { get; set; }
        public string POSTING_DATE { get; set; }
        public string VENDOR_CODE { get; set; }
        public string HEADER_TEXT { get; set; }
        public string WH_TAX_CODE { get; set; }
        public string REFRENCE_NUMBER { get; set; }
        public string VENDOR_LINE_TEXT { get; set; }
        public string GL_CODE { get; set; }
        public string AMOUNT { get; set; }
        public string TAX_CODE { get; set; }
        public string COST_CENTER { get; set; }
        public string BUSINESS_AREA { get; set; }
        public string PROFIT_CENTER { get; set; }
        public string ASSIGNMENT_NO { get; set; }
        public string GL_LINE_TEXT { get; set; }
    }
}