using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
{
    public class ZFINANCE_DOCUMENT_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFINANCE_DOCUMENT_RFC")]
        public async Task<IHttpActionResult> GetFinanceDocuments(ZFINANCE_DOCUMENT_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new
                    {
                        Status = "E",
                        Message = "Request cannot be null"
                    });
                }

                if (string.IsNullOrEmpty(request.I_COMPANY_CODE))
                {
                    return Json(new
                    {
                        Status = "E",
                        Message = "Company code is required"
                    });
                }

                RfcDestination destination = RfcDestinationManager.GetDestination(rfcConfigparameters("192.168.144.174", "210"));
                RfcRepository repository = destination.Repository;
                IRfcFunction function = repository.CreateFunction("ZFINANCE_DOCUMENT_RFC");

                function.SetValue("I_COMPANY_CODE", request.I_COMPANY_CODE);
                
                if (!string.IsNullOrEmpty(request.I_POSTING_DATE_LOW))
                {
                    function.SetValue("I_POSTING_DATE_LOW", request.I_POSTING_DATE_LOW);
                }
                
                if (!string.IsNullOrEmpty(request.I_POSTING_DATE_HIGH))
                {
                    function.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);
                }

                function.Invoke(destination);

                IRfcStructure exReturn = function.GetStructure("EX_RETURN");
                if (exReturn != null && exReturn.GetString("TYPE") == "E")
                {
                    return Json(new
                    {
                        Status = "E",
                        Message = exReturn.GetString("MESSAGE")
                    });
                }

                IRfcTable financeDocumentsTable = function.GetTable("ET_FINANCE_DOCUMENTS");
                List<Dictionary<string, object>> financeDocuments = new List<Dictionary<string, object>>();

                if (financeDocumentsTable != null)
                {
                    for (int i = 0; i < financeDocumentsTable.RowCount; i++)
                    {
                        financeDocumentsTable.CurrentIndex = i;
                        IRfcStructure row = financeDocumentsTable.CurrentRow;
                        Dictionary<string, object> rowData = new Dictionary<string, object>();

                        for (int j = 0; j < row.Metadata.FieldCount; j++)
                        {
                            RfcFieldMetadata fieldMetadata = row.Metadata[j];
                            if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                            {
                                string fieldName = fieldMetadata.Name;
                                object fieldValue = row.GetValue(fieldName);
                                rowData[fieldName] = fieldValue;
                            }
                        }
                        financeDocuments.Add(rowData);
                    }
                }

                return Json(new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_FINANCE_DOCUMENTS = financeDocuments
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Json(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }

    public class ZFINANCE_DOCUMENT_RFCRequest
    {
        public string I_COMPANY_CODE { get; set; }
        public string I_POSTING_DATE_LOW { get; set; }
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}