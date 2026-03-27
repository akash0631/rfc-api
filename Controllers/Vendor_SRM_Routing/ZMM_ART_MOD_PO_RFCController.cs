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
        public HttpResponseMessage ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Request body cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT structure
                if (request.IM_INPUT != null)
                {
                    IRfcStructure imInput = myfun.GetStructure("IM_INPUT");
                    if (!string.IsNullOrEmpty(request.IM_INPUT.PURCHASEORDER))
                        imInput.SetValue("PURCHASEORDER", request.IM_INPUT.PURCHASEORDER);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.PURCHASEORDERITEM))
                        imInput.SetValue("PURCHASEORDERITEM", request.IM_INPUT.PURCHASEORDERITEM);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MATERIAL))
                        imInput.SetValue("MATERIAL", request.IM_INPUT.MATERIAL);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.PLANT))
                        imInput.SetValue("PLANT", request.IM_INPUT.PLANT);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.STORAGELOCATON))
                        imInput.SetValue("STORAGELOCATON", request.IM_INPUT.STORAGELOCATON);
                    if (request.IM_INPUT.ORDERQUANTITY.HasValue)
                        imInput.SetValue("ORDERQUANTITY", request.IM_INPUT.ORDERQUANTITY.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.ORDERUNIT))
                        imInput.SetValue("ORDERUNIT", request.IM_INPUT.ORDERUNIT);
                    if (request.IM_INPUT.NETPRICE.HasValue)
                        imInput.SetValue("NETPRICE", request.IM_INPUT.NETPRICE.Value);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.CURRENCY))
                        imInput.SetValue("CURRENCY", request.IM_INPUT.CURRENCY);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.DELIVERYDATE))
                        imInput.SetValue("DELIVERYDATE", request.IM_INPUT.DELIVERYDATE);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.ITEMTEXT))
                        imInput.SetValue("ITEMTEXT", request.IM_INPUT.ITEMTEXT);
                }

                // Set IM_OUTPUT structure
                if (request.IM_OUTPUT != null)
                {
                    IRfcStructure imOutput = myfun.GetStructure("IM_OUTPUT");
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.PURCHASEORDER))
                        imOutput.SetValue("PURCHASEORDER", request.IM_OUTPUT.PURCHASEORDER);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.PURCHASEORDERITEM))
                        imOutput.SetValue("PURCHASEORDERITEM", request.IM_OUTPUT.PURCHASEORDERITEM);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.MATERIAL))
                        imOutput.SetValue("MATERIAL", request.IM_OUTPUT.MATERIAL);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.PLANT))
                        imOutput.SetValue("PLANT", request.IM_OUTPUT.PLANT);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.STORAGELOCATON))
                        imOutput.SetValue("STORAGELOCATON", request.IM_OUTPUT.STORAGELOCATON);
                    if (request.IM_OUTPUT.ORDERQUANTITY.HasValue)
                        imOutput.SetValue("ORDERQUANTITY", request.IM_OUTPUT.ORDERQUANTITY.Value);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.ORDERUNIT))
                        imOutput.SetValue("ORDERUNIT", request.IM_OUTPUT.ORDERUNIT);
                    if (request.IM_OUTPUT.NETPRICE.HasValue)
                        imOutput.SetValue("NETPRICE", request.IM_OUTPUT.NETPRICE.Value);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.CURRENCY))
                        imOutput.SetValue("CURRENCY", request.IM_OUTPUT.CURRENCY);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.DELIVERYDATE))
                        imOutput.SetValue("DELIVERYDATE", request.IM_OUTPUT.DELIVERYDATE);
                    if (!string.IsNullOrEmpty(request.IM_OUTPUT.ITEMTEXT))
                        imOutput.SetValue("ITEMTEXT", request.IM_OUTPUT.ITEMTEXT);
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetValue("TYPE").ToString();
                string returnMessage = EX_RETURN.GetValue("MESSAGE").ToString();

                if (returnType == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = returnMessage });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "S", Message = returnMessage });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class ZMM_ART_MOD_PO_RFCRequest
    {
        public ZMM_PO_ART_ST IM_INPUT { get; set; }
        public ZMM_PO_ART_OUT_ST IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_ST
    {
        public string PURCHASEORDER { get; set; }
        public string PURCHASEORDERITEM { get; set; }
        public string MATERIAL { get; set; }
        public string PLANT { get; set; }
        public string STORAGELOCATON { get; set; }
        public decimal? ORDERQUANTITY { get; set; }
        public string ORDERUNIT { get; set; }
        public decimal? NETPRICE { get; set; }
        public string CURRENCY { get; set; }
        public string DELIVERYDATE { get; set; }
        public string ITEMTEXT { get; set; }
    }

    public class ZMM_PO_ART_OUT_ST
    {
        public string PURCHASEORDER { get; set; }
        public string PURCHASEORDERITEM { get; set; }
        public string MATERIAL { get; set; }
        public string PLANT { get; set; }
        public string STORAGELOCATON { get; set; }
        public decimal? ORDERQUANTITY { get; set; }
        public string ORDERUNIT { get; set; }
        public decimal? NETPRICE { get; set; }
        public string CURRENCY { get; set; }
        public string DELIVERYDATE { get; set; }
        public string ITEMTEXT { get; set; }
    }
}