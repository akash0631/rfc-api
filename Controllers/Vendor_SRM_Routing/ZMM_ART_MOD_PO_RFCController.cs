using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using System.Linq;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor
{
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public async Task<object> ExecuteRFC(ZMM_ART_MOD_PO_RFC_Request request)
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                myfun.SetValue("EBELN", request.EBELN);
                myfun.SetValue("MATNR", request.MATNR);
                myfun.SetValue("COLOR", request.COLOR);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                if (EX_RETURN != null && EX_RETURN.GetString("TYPE") == "E")
                {
                    return new
                    {
                        Status = "E",
                        Message = EX_RETURN.GetString("MESSAGE")
                    };
                }

                IRfcTable tbl = myfun.GetTable("IM_OUTPUT");
                var outputData = new List<Dictionary<string, object>>();

                if (tbl != null)
                {
                    outputData = tbl.AsEnumerable().Select(row =>
                    {
                        var dict = new Dictionary<string, object>();
                        for (int i = 0; i < row.Metadata.FieldCount; i++)
                        {
                            var field = row.Metadata[i];
                            if (field.DataType != RfcDataType.STRUCTURE && field.DataType != RfcDataType.TABLE)
                            {
                                dict[field.Name] = row.GetValue(field.Name);
                            }
                        }
                        return dict;
                    }).ToList();
                }

                return new
                {
                    Status = "S",
                    Message = "Success",
                    Data = new
                    {
                        IM_OUTPUT = outputData
                    }
                };
            }
            catch (RfcAbapException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message
                };
            }
            catch (RfcCommunicationException ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Status = "E",
                    Message = ex.Message
                };
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