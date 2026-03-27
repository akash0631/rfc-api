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
    public class ZFI_PI_DATA_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_PI_DATA_RFC")]
        public async Task<HttpResponseMessage> ProcessFinancePostingData(ZFI_PI_DATA_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_PI_DATA_RFC");

                IRfcTable itPostingLowTable = myfun.GetTable("IT_POSTING_LOW");
                itPostingLowTable.Clear();

                if (request.IT_POSTING_LOW != null)
                {
                    foreach (var postingItem in request.IT_POSTING_LOW)
                    {
                        itPostingLowTable.Append();
                        IRfcStructure row = itPostingLowTable.CurrentRow;
                        
                        foreach (var property in typeof(PostingLowItem).GetProperties())
                        {
                            var value = property.GetValue(postingItem);
                            if (value != null)
                            {
                                row.SetValue(property.Name.ToUpper(), value.ToString());
                            }
                        }
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    var errorResponse = new ZFI_PI_DATA_RFCResponse
                    {
                        Status = "E",
                        Message = returnMessage
                    };
                    return Request.CreateResponse(HttpStatusCode.BadRequest, errorResponse);
                }

                var successResponse = new ZFI_PI_DATA_RFCResponse
                {
                    Status = returnType,
                    Message = returnMessage
                };

                return Request.CreateResponse(HttpStatusCode.OK, successResponse);
            }
            catch (RfcAbapException ex)
            {
                var errorResponse = new ZFI_PI_DATA_RFCResponse
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
            catch (RfcCommunicationException ex)
            {
                var errorResponse = new ZFI_PI_DATA_RFCResponse
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = new ZFI_PI_DATA_RFCResponse
                {
                    Status = "E",
                    Message = ex.Message
                };
                return Request.CreateResponse(HttpStatusCode.InternalServerError, errorResponse);
            }
        }
    }

    public class ZFI_PI_DATA_RFCRequest
    {
        public List<PostingLowItem> IT_POSTING_LOW { get; set; }
    }

    public class ZFI_PI_DATA_RFCResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class PostingLowItem
    {
        public string BUKRS { get; set; }
        public string BLART { get; set; }
        public string BLDAT { get; set; }
        public string BUDAT { get; set; }
        public string XBLNR { get; set; }
        public string BKTXT { get; set; }
        public string WAERS { get; set; }
        public string KURSF { get; set; }
        public string HKONT { get; set; }
        public string KOSTL { get; set; }
        public string AUFNR { get; set; }
        public string WRBTR { get; set; }
        public string DMBTR { get; set; }
        public string MWSKZ { get; set; }
        public string SGTXT { get; set; }
        public string LIFNR { get; set; }
        public string KUNNR { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string VBELN { get; set; }
        public string POSNR { get; set; }
        public string PRCTR { get; set; }
        public string SEGMENT { get; set; }
        public string PSEGMENT { get; set; }
        public string ZUONR { get; set; }
        public string FKBER { get; set; }
        public string XREF1 { get; set; }
        public string XREF2 { get; set; }
        public string XREF3 { get; set; }
        public string BSCHL { get; set; }
        public string SHKZG { get; set; }
    }
}