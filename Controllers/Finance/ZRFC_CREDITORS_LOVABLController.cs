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
    public class ZRFC_CREDITORS_LOVABLController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZRFC_CREDITORS_LOVABLRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Validate_GRC Authenticate = new Validate_GRC();
                    if ( request.VENDOR != null)
                    {

                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZRFC_CREDITORS_LOVABL");

                        myfun.SetValue("COMPANY_CODE", request.COMPANY_CODE);
                        myfun.SetValue("VENDOR", request.VENDOR);
                        myfun.SetValue("POSTING_DATE", request.POSTING_DATE);

                        
                       

                        myfun.Invoke(dest);
                        IRfcTable IrfTable = myfun.GetTable("LT_DATA");
                        //IRfcTable IrfTable1 = myfun.GetTable("ET_EAN_DATA");

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
                            //return Request.CreateResponse(HttpStatusCode.OK, new
                            //{
                            //    Status = true,
                            //    Message = "" + SAP_Message + ""

                            //});

                            //int i = 0;
                            //var etdata = IrfTable.AsEnumerable().ToList();
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
                            //var eteandata = IrfTable.AsEnumerable().Select(row => new
                            //{
                            //    BLART = row.GetString("BLART"),

                            //}).ToList();

                            //var eteandata
                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Status = true,
                                Message = "" + SAP_Message + "",
                                Data = new
                                {
                                    ET_Data = eteandata

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
    public class ZRFC_CREDITORS_LOVABLRequest
    {
        public string COMPANY_CODE { get; set; }
        public string POSTING_DATE { get; set; }
        public string VENDOR { get; set; }
     
        
    }
}