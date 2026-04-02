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
        public async Task<HttpResponseMessage> UploadExpenseData(ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                // Set IMPORT parameters
                IRfcStructure imInput = myfun.GetStructure("IM_INPUT");
                if (!string.IsNullOrEmpty(request.COMPANY_CODE))
                    imInput.SetValue("COMPANY_CODE", request.COMPANY_CODE);
                if (!string.IsNullOrEmpty(request.INVOICE_DATE))
                    imInput.SetValue("INVOICE_DATE", request.INVOICE_DATE);
                if (!string.IsNullOrEmpty(request.POSTING_DATE))
                    imInput.SetValue("POSTING_DATE", request.POSTING_DATE);
                if (!string.IsNullOrEmpty(request.VENDOR_CODE))
                    imInput.SetValue("VENDOR_CODE", request.VENDOR_CODE);
                if (!string.IsNullOrEmpty(request.HEADER_TEXT))
                    imInput.SetValue("HEADER_TEXT", request.HEADER_TEXT);
                if (!string.IsNullOrEmpty(request.WH_TAX_CODE))
                    imInput.SetValue("WH_TAX_CODE", request.WH_TAX_CODE);
                if (!string.IsNullOrEmpty(request.REFRENCE_NUMBER))
                    imInput.SetValue("REFRENCE_NUMBER", request.REFRENCE_NUMBER);
                if (!string.IsNullOrEmpty(request.VENDOR_LINE_TEXT))
                    imInput.SetValue("VENDOR_LINE_TEXT", request.VENDOR_LINE_TEXT);
                if (!string.IsNullOrEmpty(request.GL_CODE))
                    imInput.SetValue("GL_CODE", request.GL_CODE);
                if (!string.IsNullOrEmpty(request.AMOUNT))
                    imInput.SetValue("AMOUNT", request.AMOUNT);
                if (!string.IsNullOrEmpty(request.TAX_CODE))
                    imInput.SetValue("TAX_CODE", request.TAX_CODE);
                if (!string.IsNullOrEmpty(request.COST_CENTER))
                    imInput.SetValue("COST_CENTER", request.COST_CENTER);
                if (!string.IsNullOrEmpty(request.BUSINESS_AREA))
                    imInput.SetValue("BUSINESS_AREA", request.BUSINESS_AREA);
                if (!string.IsNullOrEmpty(request.PROFIT_CENTER))
                    imInput.SetValue("PROFIT_CENTER", request.PROFIT_CENTER);
                if (!string.IsNullOrEmpty(request.ASSIGNMENT_NO))
                    imInput.SetValue("ASSIGNMENT_NO", request.ASSIGNMENT_NO);
                if (!string.IsNullOrEmpty(request.GL_LINE_TEXT))
                    imInput.SetValue("GL_LINE_TEXT", request.GL_LINE_TEXT);

                myfun.Invoke(dest);

                IRfcTable exReturnTable = myfun.GetTable("EX_RETURN");
                
                var returnData = new List<dynamic>();
                foreach (IRfcStructure row in exReturnTable)
                {
                    var rowData = new Dictionary<string, object>();
                    for (int fi = 0; fi < row.Metadata.FieldCount; fi++)
                    {
                        RfcFieldMetadata field = row.Metadata[fi];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            rowData[field.Name] = row.GetValue(field.Name)?.ToString();
                        }
                    }
                    returnData.Add(rowData);
                    
                    // Check for error type
                    var messageType = row.GetValue("TYPE")?.ToString();
                    if (messageType == "E")
                    {
                        var errorMessage = row.GetValue("MESSAGE")?.ToString();
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = "E",
                            Message = errorMessage ?? "Error occurred during expense upload processing",
                            Data = new { EX_RETURN = returnData }
                        });
                    }
                }

                var response = new
                {
                    Status = "S",
                    Message = "Expense data uploaded successfully",
                    Data = new { EX_RETURN = returnData }
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }

    public class ZFI_EXP_UPLOAD_RFCRequest
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