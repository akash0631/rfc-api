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
    public class ZVND_PUTWAY_PALETTE_VAL_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZVND_PUTWAY_PALETTE_VAL_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request.IM_USER != null)
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZVND_PUTWAY_PALETTE_VAL_RFC");

                        myfun.SetValue("IM_USER",  request.IM_USER);
                        myfun.SetValue("IM_PLANT", request.IM_PLANT);
                        myfun.SetValue("IM_BIN",   request.IM_BIN);
                        myfun.SetValue("IM_PALL",  request.IM_PALL);

                        myfun.Invoke(dest);

                        IRfcTable IrfTable = myfun.GetTable("ET_DATA");

                        IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");

                        string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();

                        if (SAP_TYPE == "E")
                        {
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = false,
                                Message = "" + SAP_Message + ""
                            });
                        }
                        else
                        {
                            var meta = IrfTable.Metadata.LineType;

                            var etdata = IrfTable.AsEnumerable().Select(r =>
                            {
                                var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                                for (int i = 0; i < meta.FieldCount; i++)
                                {
                                    var f = meta[i];

                                    if (f.DataType == RfcDataType.STRUCTURE || f.DataType == RfcDataType.TABLE)
                                        continue;

                                    try
                                    {
                                        d[f.Name] = r.GetString(f.Name);
                                    }
                                    catch
                                    {
                                        d[f.Name] = null;
                                    }
                                }

                                return d;
                            }).ToList();

                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = true,
                                Message = "" + SAP_Message + "",
                                Data = new
                                {
                                    ET_DATA = etdata
                                }
                            });
                        }
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = false,
                            Message = "Request Not Valid"
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Status = false,
                        Message = ex.Message
                    });
                }
            });
        }
    }

    public class ZVND_PUTWAY_PALETTE_VAL_RFCRequest
    {
        /// <summary>TYPE: WWWOBJID — SAP User ID</summary>
        public string IM_USER { get; set; }

        /// <summary>TYPE: WERKS_D — Plant</summary>
        public string IM_PLANT { get; set; }

        /// <summary>TYPE: ZEXT_HU — Handling Unit / BIN reference</summary>
        public string IM_BIN { get; set; }

        /// <summary>TYPE: ZZPALETTE — Palette number to validate</summary>
        public string IM_PALL { get; set; }
    }
}
