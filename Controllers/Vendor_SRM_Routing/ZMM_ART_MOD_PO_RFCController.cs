using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor
{
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public async Task<HttpResponseMessage> ModifyArticlePO([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request body cannot be null"
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT table parameter
                if (request.IM_INPUT != null)
                {
                    IRfcTable inputTable = myfun.GetTable("IM_INPUT");
                    foreach (var item in request.IM_INPUT)
                    {
                        IRfcStructure row = inputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(item.PO_NUMBER))
                            row.SetValue("PO_NUMBER", item.PO_NUMBER);
                        if (!string.IsNullOrEmpty(item.PO_ITEM))
                            row.SetValue("PO_ITEM", item.PO_ITEM);
                        if (!string.IsNullOrEmpty(item.ARTICLE))
                            row.SetValue("ARTICLE", item.ARTICLE);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            row.SetValue("COLOR", item.COLOR);
                        if (item.QUANTITY.HasValue)
                            row.SetValue("QUANTITY", item.QUANTITY.Value);
                        if (item.UNIT_PRICE.HasValue)
                            row.SetValue("UNIT_PRICE", item.UNIT_PRICE.Value);
                        if (!string.IsNullOrEmpty(item.CURRENCY))
                            row.SetValue("CURRENCY", item.CURRENCY);
                        if (!string.IsNullOrEmpty(item.DELIVERY_DATE))
                            row.SetValue("DELIVERY_DATE", item.DELIVERY_DATE);
                        
                        inputTable.Append(row);
                    }
                }

                // Set IM_OUTPUT table parameter
                if (request.IM_OUTPUT != null)
                {
                    IRfcTable outputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        IRfcStructure row = outputTable.Metadata.LineType.CreateStructure();
                        
                        if (!string.IsNullOrEmpty(item.PO_NUMBER))
                            row.SetValue("PO_NUMBER", item.PO_NUMBER);
                        if (!string.IsNullOrEmpty(item.PO_ITEM))
                            row.SetValue("PO_ITEM", item.PO_ITEM);
                        if (!string.IsNullOrEmpty(item.ARTICLE))
                            row.SetValue("ARTICLE", item.ARTICLE);
                        if (!string.IsNullOrEmpty(item.COLOR))
                            row.SetValue("COLOR", item.COLOR);
                        if (!string.IsNullOrEmpty(item.STATUS))
                            row.SetValue("STATUS", item.STATUS);
                        if (!string.IsNullOrEmpty(item.MESSAGE))
                            row.SetValue("MESSAGE", item.MESSAGE);
                        
                        outputTable.Append(row);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = returnMessage
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = returnMessage
                });
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

    public class ZMM_ART_MOD_PO_RFCRequest
    {
        public List<ZMM_PO_ART_Input> IM_INPUT { get; set; }
        public List<ZMM_PO_ART_Output> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_Input
    {
        public string PO_NUMBER { get; set; }
        public string PO_ITEM { get; set; }
        public string ARTICLE { get; set; }
        public string COLOR { get; set; }
        public decimal? QUANTITY { get; set; }
        public decimal? UNIT_PRICE { get; set; }
        public string CURRENCY { get; set; }
        public string DELIVERY_DATE { get; set; }
    }

    public class ZMM_PO_ART_Output
    {
        public string PO_NUMBER { get; set; }
        public string PO_ITEM { get; set; }
        public string ARTICLE { get; set; }
        public string COLOR { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
    }
}