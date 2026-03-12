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
    public class ZADVANCE_PAYMENT_RFCController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZADVANCE_PAYMENT_RFCRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request.I_COMPANY_CODE != null)
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;

                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZADVANCE_PAYMENT_RFC");

                        myfun.SetValue("I_COMPANY_CODE",      request.I_COMPANY_CODE);
                        myfun.SetValue("I_POSTING_DATE_LOW",  request.I_POSTING_DATE_LOW);
                        myfun.SetValue("I_POSTING_DATE_HIGH", request.I_POSTING_DATE_HIGH);

                        myfun.Invoke(dest);

                        IRfcTable IrfTable = myfun.GetTable("IT_FINAL");

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
                                    IT_FINAL = etdata
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

    public class ZADVANCE_PAYMENT_RFCRequest
    {
        /// <summary>TYPE: BUKRS — Company Code</summary>
        public string I_COMPANY_CODE { get; set; }

        /// <summary>TYPE: BUDAT — Posting Date From (YYYYMMDD)</summary>
        public string I_POSTING_DATE_LOW { get; set; }

        /// <summary>TYPE: BUDAT — Posting Date To (YYYYMMDD)</summary>
        public string I_POSTING_DATE_HIGH { get; set; }
    }
}
