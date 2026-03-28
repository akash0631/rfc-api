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
        public async Task<HttpResponseMessage> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_RFC_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request body cannot be null",
                        Data = (object)null
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                if (!string.IsNullOrEmpty(request.EBELN))
                    myfun.SetValue("EBELN", request.EBELN);
                if (!string.IsNullOrEmpty(request.MATNR))
                    myfun.SetValue("MATNR", request.MATNR);
                if (!string.IsNullOrEmpty(request.COLOR))
                    myfun.SetValue("COLOR", request.COLOR);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = (object)null
                    });
                }

                IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                var imOutputData = new List<Dictionary<string, object>>();

                if (imOutputTable != null)
                {
                    foreach (IRfcStructure row in imOutputTable)
                    {
                        var rowData = new Dictionary<string, object>();
                        for (int i = 0; i < row.Metadata.FieldCount; i++)
                        {
                            var fieldMetadata = row.Metadata.GetFieldMetadata(i);
                            if (fieldMetadata.DataType != RfcDataType.STRUCTURE && fieldMetadata.DataType != RfcDataType.TABLE)
                            {
                                rowData[fieldMetadata.Name] = row.GetValue(fieldMetadata.Name);
                            }
                        }
                        imOutputData.Add(rowData);
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        IM_OUTPUT = imOutputData
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "E",
                    Message = ex.Message,
                    Data = (object)null
                });
            }
        }
    }

    public class ZMM_ART_MOD_PO_RFC_Request
    {
        public string EBELN { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
    }
}