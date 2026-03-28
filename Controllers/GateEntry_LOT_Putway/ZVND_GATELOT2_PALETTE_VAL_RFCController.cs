using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.GateEntry
{
    public class ZVND_GATELOT2_PALETTE_VAL_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_GATELOT2_PALETTE_VAL_RFC")]
        public HttpResponseMessage ZVND_GATELOT2_PALETTE_VAL_RFC([FromBody] ZVND_GATELOT2_PALETTE_VAL_RFCRequest request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_GATELOT2_PALETTE_VAL_RFC");

                myfun.SetValue("IM_USER", request.IM_USER);
                myfun.SetValue("IM_PLANT", request.IM_PLANT);
                myfun.SetValue("IM_PICKLIST", request.IM_PICKLIST);
                myfun.SetValue("IM_BIN", request.IM_BIN);
                myfun.SetValue("IM_PALL", request.IM_PALL);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN.GetString("TYPE") == "E")
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE"),
                        Data = new { }
                    });
                }

                IRfcTable tbl = myfun.GetTable("ET_BIN");
                var etBinList = new List<Dictionary<string, object>>();

                foreach (IRfcStructure row in tbl)
                {
                    var rowData = new Dictionary<string, object>();
                    for (int i = 0; i < row.Metadata.FieldCount; i++)
                    {
                        var field = row.Metadata[i];
                        if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                        {
                            rowData[field.Name] = row.GetValue(field.Name);
                        }
                    }
                    etBinList.Add(rowData);
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        ET_BIN = etBinList
                    }
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

    public class ZVND_GATELOT2_PALETTE_VAL_RFCRequest
    {
        public string IM_USER { get; set; }
        public string IM_PLANT { get; set; }
        public string IM_PICKLIST { get; set; }
        public string IM_BIN { get; set; }
        public string IM_PALL { get; set; }
    }
}