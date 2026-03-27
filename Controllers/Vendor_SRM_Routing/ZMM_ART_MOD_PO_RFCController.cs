using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor
{
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public HttpResponseMessage ModifyArticleInPurchaseOrder([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request body is required",
                        Data = new { }
                    });
                }

                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = errors,
                        Data = new { }
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                myfun.SetValue("IM_INPUT", request.IM_INPUT);
                myfun.SetValue("IM_OUTPUT", request.IM_OUTPUT);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetValue("TYPE").ToString() == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetValue("MESSAGE").ToString(),
                        Data = new { }
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_PO_ARTICLE_MOD");

                var tableData = tbl.AsEnumerable().Select(row =>
                {
                    var rowData = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        var fieldName = field.Name;
                        var fieldValue = row.GetValue(fieldName);
                        
                        if (fieldValue != null)
                        {
                            rowData[fieldName] = fieldValue.ToString();
                        }
                        else
                        {
                            rowData[fieldName] = "";
                        }
                    }
                    return rowData;
                }).ToList();

                var response = new
                {
                    Status = "S",
                    Message = "Article/Material in Purchase Order modified successfully",
                    Data = new
                    {
                        ET_PO_ARTICLE_MOD = tableData
                    }
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = new { }
                });
            }
        }
    }

    public class ZMM_ART_MOD_PO_RFCRequest
    {
        [Required(ErrorMessage = "IM_INPUT is required")]
        public string IM_INPUT { get; set; }

        [Required(ErrorMessage = "IM_OUTPUT is required")]
        public string IM_OUTPUT { get; set; }
    }
}
