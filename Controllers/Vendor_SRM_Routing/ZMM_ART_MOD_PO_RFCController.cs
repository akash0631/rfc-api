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
        public async Task<HttpResponseMessage> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = "E", Message = "Invalid request data" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT table
                if (request.IM_INPUT != null && request.IM_INPUT.Count > 0)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IStructure row = imInputTable.Metadata.LineType.CreateStructure();
                        foreach (var property in inputItem.GetType().GetProperties())
                        {
                            var value = property.GetValue(inputItem);
                            if (value != null)
                            {
                                row.SetValue(property.Name.ToUpper(), value.ToString());
                            }
                        }
                        imInputTable.Append(row);
                    }
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IStructure row = imOutputTable.Metadata.LineType.CreateStructure();
                        foreach (var property in outputItem.GetType().GetProperties())
                        {
                            var value = property.GetValue(outputItem);
                            if (value != null)
                            {
                                row.SetValue(property.Name.ToUpper(), value.ToString());
                            }
                        }
                        imOutputTable.Append(row);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

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

    public class ZMM_ART_MOD_PO_Request
    {
        public List<ZMM_PO_ART_TT> IM_INPUT { get; set; }
        public List<ZMM_PO_ART_OUT_TT> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_TT
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
        public string EINDT { get; set; }
    }

    public class ZMM_PO_ART_OUT_TT
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string MENGE { get; set; }
        public string MEINS { get; set; }
        public string NETPR { get; set; }
        public string PEINH { get; set; }
        public string EINDT { get; set; }
        public string MESSAGE { get; set; }
        public string STATUS { get; set; }
    }
}