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
    public class ZFI_PI_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_PI_DATA_RFC")]
        public IHttpActionResult ProcessPurchaseInvoiceData(ZFI_PI_DATA_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");

                // Set table parameters
                if (request.IT_POSTING_LOW != null && request.IT_POSTING_LOW.Any())
                {
                    IRfcTable postingLowTable = myfun.GetTable("IT_POSTING_LOW");
                    foreach (var item in request.IT_POSTING_LOW)
                    {
                        postingLowTable.Append();
                        var properties = item.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(item);
                            if (value != null)
                            {
                                postingLowTable.SetValue(prop.Name, value);
                            }
                        }
                    }
                }

                if (request.IT_POSTING_HIGH != null && request.IT_POSTING_HIGH.Any())
                {
                    IRfcTable postingHighTable = myfun.GetTable("IT_POSTING_HIGH");
                    foreach (var item in request.IT_POSTING_HIGH)
                    {
                        postingHighTable.Append();
                        var properties = item.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(item);
                            if (value != null)
                            {
                                postingHighTable.SetValue(prop.Name, value);
                            }
                        }
                    }
                }

                // Set scalar parameters
                if (!string.IsNullOrEmpty(request.INV_DOC_NO))
                    myfun.SetValue("INV_DOC_NO", request.INV_DOC_NO);
                
                if (!string.IsNullOrEmpty(request.FISCAL_YEAR))
                    myfun.SetValue("FISCAL_YEAR", request.FISCAL_YEAR);
                
                if (!string.IsNullOrEmpty(request.DOCUMENT_DATE))
                    myfun.SetValue("DOCUMENT_DATE", request.DOCUMENT_DATE);
                
                if (!string.IsNullOrEmpty(request.POSTING_DATE))
                    myfun.SetValue("POSTING_DATE", request.POSTING_DATE);
                
                if (!string.IsNullOrEmpty(request.REFERENCE))
                    myfun.SetValue("REFERENCE", request.REFERENCE);
                
                if (!string.IsNullOrEmpty(request.INVOICING_PARTY))
                    myfun.SetValue("INVOICING_PARTY", request.INVOICING_PARTY);
                
                if (request.GROSS_INV_AMNT.HasValue)
                    myfun.SetValue("GROSS_INV_AMNT", request.GROSS_INV_AMNT.Value);
                
                if (request.VALUE_ADDED_TAX.HasValue)
                    myfun.SetValue("VALUE_ADDED_TAX", request.VALUE_ADDED_TAX.Value);
                
                if (!string.IsNullOrEmpty(request.TAX_CODE))
                    myfun.SetValue("TAX_CODE", request.TAX_CODE);
                
                if (!string.IsNullOrEmpty(request.PURCHASING_DOC))
                    myfun.SetValue("PURCHASING_DOC", request.PURCHASING_DOC);
                
                if (!string.IsNullOrEmpty(request.REFERENCE_DOC))
                    myfun.SetValue("REFERENCE_DOC", request.REFERENCE_DOC);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return Ok(new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = new { IT_FINAL = new List<object>() }
                    });
                }

                IRfcTable itFinalTable = myfun.GetTable("IT_FINAL");
                var resultData = new List<Dictionary<string, object>>();

                if (itFinalTable != null)
                {
                    resultData = itFinalTable.AsEnumerable().Select(row =>
                    {
                        var rowDict = new Dictionary<string, object>();
                        var metadata = row.GetMetadata();
                        
                        for (int i = 0; i < metadata.FieldCount; i++)
                        {
                            var fieldMetadata = metadata[i];
                            if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                            {
                                var fieldName = fieldMetadata.Name;
                                var fieldValue = row.GetValue(fieldName);
                                rowDict[fieldName] = fieldValue;
                            }
                        }
                        
                        return rowDict;
                    }).ToList();
                }

                return Ok(new
                {
                    Status = "S",
                    Message = "Purchase invoice data processed successfully",
                    Data = new { IT_FINAL = resultData }
                });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "E",
                    Message = ex.Message
                });
            }
        }
    }

    public class ZFI_PI_DATA_Request
    {
        public List<object> IT_POSTING_LOW { get; set; }
        public List<object> IT_POSTING_HIGH { get; set; }
        public string INV_DOC_NO { get; set; }
        public string FISCAL_YEAR { get; set; }
        public string DOCUMENT_DATE { get; set; }
        public string POSTING_DATE { get; set; }
        public string REFERENCE { get; set; }
        public string INVOICING_PARTY { get; set; }
        public decimal? GROSS_INV_AMNT { get; set; }
        public decimal? VALUE_ADDED_TAX { get; set; }
        public string TAX_CODE { get; set; }
        public string PURCHASING_DOC { get; set; }
        public string REFERENCE_DOC { get; set; }
    }
}