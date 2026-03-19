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
    public class ZWM_SCAN_HUController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZWM_SCAN_HURequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if ( request.IM_PLANT != null)
                    {

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZWM_SCAN_HU");

                        myfun.SetValue("IM_HU", request.IM_HU);
                        myfun.SetValue("IM_USER", request.IM_USER);
                        myfun.SetValue("IM_PLANT", request.IM_PLANT);

                        
                       

                        myfun.Invoke(dest);
                        IRfcTable IrfTable = myfun.GetTable("ET_ATICLES");
                        IRfcTable IrfTable1 = myfun.GetTable("ET_EAN");

                        IRfcStructure E_RETURN = myfun.GetStructure("ET_ERROR");

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

                            var eteandata = IrfTable.AsEnumerable().Select(r =>
                            {
                                var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                                for (int i = 0; i < meta.FieldCount; i++)
                                {
                                    var f = meta[i];

                                    // Skip deep types
                                    if (f.DataType == RfcDataType.STRUCTURE || f.DataType == RfcDataType.TABLE)
                                        continue;

                                    try
                                    {
                                        d[f.Name] = r.GetString(f.Name); // safe generic representation for most simple types
                                    }
                                    catch
                                    {
                                        d[f.Name] = null;
                                    }
                                }

                                return d;
                            }).ToList();
                            var eteandata1 = IrfTable1.AsEnumerable().Select(r =>
                            {
                                var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                                for (int i = 0; i < meta.FieldCount; i++)
                                {
                                    var f = meta[i];

                                    // Skip deep types
                                    if (f.DataType == RfcDataType.STRUCTURE || f.DataType == RfcDataType.TABLE)
                                        continue;

                                    try
                                    {
                                        d[f.Name] = r.GetString(f.Name); // safe generic representation for most simple types
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
                                    ET_ARTICLES = eteandata,
                                    ET_EAN = eteandata1

                                }
                                //Data = new { 
                                //    ET_Data = etdata,
                                //    ET_EAN_DATA = eteandata
                                //}

                            });
                        }
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = true,
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
    public class ZWM_SCAN_HURequest
    {
        public string IM_HU { get; set; }
        public string IM_USER { get; set; }
        public string IM_PLANT { get; set; }
     
        
    }
}