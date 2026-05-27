using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.MM
{
    /// <summary>
    /// RFC: ZMM_ART_CREATION_RFC
    /// Purpose: Create SAP Article masters in bulk via FM ZMM_ART_CREATION_RFC.
    ///          FM loops IM_DATA rows, builds article via ZCL_MM_ARTICLE_FINAL=>Z_FILL_DATA_BAPI
    ///          (BAPI_MATERIAL_SAVEDATA under the hood), returns SAP article number + S/E per row.
    ///
    /// IMPORT:  IM_DATA  (ZTT_ART_CRT)     - table of article-creation rows.
    ///                   Line type ZMM_ART_CRT (84 fields).
    ///                   Required base fields: SUB_DIV, MC_CD, VENDOR, HSN_CODE, DSG_NO,
    ///                                         MRP, SEASON, ARTICLE_DES1, PRICE_BAND_CATEGORY,
    ///                                         NET_WEIGHT
    ///                   Optional: 74 MVGR attributes (M_*).
    ///                   Pass as List&lt;Dictionary&lt;string,string&gt;&gt; for forward-compat
    ///                   (DDIC line type still evolving on S4D).
    /// EXPORT:  EX_DATA  (ZTT_ART_CRT_RET) - table of return rows.
    ///                   Fields: SAP_ART (material number), MSG_TYP (S/E), MESSAGE.
    ///
    /// SAP Target: DEVELOPMENT (192.168.144.174 / Client 210 / S4D)
    ///             ZCL_MM_ARTICLE_FINAL class lives on S4D only — swap to
    ///             rfcConfigparametersquality() / rfcConfigparametersproduction()
    ///             after Bhavesh STMS-promotes the class + FM.
    ///
    /// Concurrent calls serialized via process-wide semaphore (FM uses SAP memory IDs
    /// inside ZCL_MM_ARTICLE_FINAL — concurrent invocations could race).
    /// </summary>
    public class ZMM_ART_CREATION_RFCController : BaseController
    {
        private static readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private const int GateTimeoutSeconds = 120;

        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] ZMM_ART_CREATION_RFCRequest request)
        {
            if (request == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "Request body is required." });

            if (request.IM_DATA == null || request.IM_DATA.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = "IM_DATA must contain at least one article row." });

            bool entered = false;
            try
            {
                entered = await _gate.WaitAsync(TimeSpan.FromSeconds(GateTimeoutSeconds));
                if (!entered)
                    return Request.CreateResponse((HttpStatusCode)429, new { Status = false, Message = "Article creation queue is busy; please retry." });

                return await Task.Run(() =>
                {
                    try
                    {
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparameters(); // DEV (.174 / Client 210 / S4D)
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        IRfcFunction myfun = dest.Repository.CreateFunction("ZMM_ART_CREATION_RFC");

                        IRfcTable imData = myfun.GetTable("IM_DATA");
                        RfcStructureMetadata lineMeta = imData.Metadata.LineType;

                        foreach (var row in request.IM_DATA)
                        {
                            imData.Append();
                            if (row == null) continue;

                            foreach (var kv in row)
                            {
                                if (string.IsNullOrEmpty(kv.Key)) continue;
                                string fieldName = kv.Key.ToUpperInvariant();

                                bool fieldExists = false;
                                for (int i = 0; i < lineMeta.FieldCount; i++)
                                {
                                    if (string.Equals(lineMeta[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        fieldExists = true;
                                        break;
                                    }
                                }
                                if (!fieldExists) continue; // silently skip unknown fields (forward-compat)

                                imData.SetValue(fieldName, kv.Value ?? string.Empty);
                            }
                        }

                        myfun.Invoke(dest);

                        var results = new List<ZMM_ART_CREATION_ResultRow>();
                        IRfcTable exData = myfun.GetTable("EX_DATA");
                        for (int i = 0; i < exData.RowCount; i++)
                        {
                            exData.CurrentIndex = i;
                            results.Add(new ZMM_ART_CREATION_ResultRow
                            {
                                SAP_ART = exData.GetValue("SAP_ART")?.ToString() ?? string.Empty,
                                MSG_TYP = exData.GetValue("MSG_TYP")?.ToString() ?? string.Empty,
                                MESSAGE = exData.GetValue("MESSAGE")?.ToString() ?? string.Empty
                            });
                        }

                        int successCount = 0;
                        int errorCount = 0;
                        foreach (var r in results)
                        {
                            if (r.MSG_TYP == "S") successCount++;
                            else if (r.MSG_TYP == "E") errorCount++;
                        }

                        return Request.CreateResponse(HttpStatusCode.OK, new
                        {
                            Status = errorCount == 0,
                            Message = $"{successCount} created, {errorCount} failed (of {results.Count} rows).",
                            SuccessCount = successCount,
                            ErrorCount = errorCount,
                            EX_DATA = results
                        });
                    }
                    catch (Exception ex)
                    {
                        return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = false, Message = ex.Message });
                    }
                });
            }
            finally
            {
                if (entered) _gate.Release();
            }
        }
    }

    public class ZMM_ART_CREATION_RFCRequest
    {
        public List<Dictionary<string, string>> IM_DATA { get; set; }
    }

    public class ZMM_ART_CREATION_ResultRow
    {
        public string SAP_ART { get; set; }
        public string MSG_TYP { get; set; }
        public string MESSAGE { get; set; }
    }
}
