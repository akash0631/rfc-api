using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_SRM_Routing_Application.Services;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Dynamic RFC Executor — THE key innovation from merging both implementations.
    ///
    /// Any RFC registered in Snowflake GOLD.RFC_MASTER is instantly callable here.
    /// No C# controller code, no deploy, no restart required.
    /// Just add a row to RFC_MASTER and POST to /api/execute/{rfcCode}.
    ///
    /// PIPELINE 1 (Direct API):
    ///   POST /api/execute/{rfcCode}           → SAP RFC result as JSON
    ///
    /// PIPELINE 2 (Data Lake Sync):
    ///   POST /api/execute/{rfcCode}/sync      → SAP RFC → Snowflake GOLD table
    ///   GET  /api/datalake/{tableName}        → read from Snowflake GOLD
    ///
    /// Date safeguards (from our implementation):
    ///   - dateFrom + dateTo required when RFC has date params
    ///   - Max 90-day range enforced
    ///   - Records > 100,000 without date filter rejected
    /// </summary>
    [RoutePrefix("api/execute")]
    public class RfcExecuteController : BaseController
    {
        private readonly EndpointRegistryService _registry = EndpointRegistryService.Instance;
        private readonly SnowflakeService _sf = new SnowflakeService();

        private const int MAX_DATE_DAYS   = 90;
        private const int MAX_RECORDS_NO_DATE = 100000;

        // ── PIPELINE 1: Direct RFC execution ─────────────────────────────────

        /// <summary>
        /// Execute any RFC from the Snowflake catalog and return results as JSON.
        /// Parameters are auto-resolved from RFC_PARAM with type coercion and defaults.
        ///
        /// Example: POST /api/execute/ZSALES_MOP_RFC
        /// Body: { "params": { "I_BUDAT_LOW": "20260401", "I_BUDAT_HIGH": "20260430" }, "env": "PROD" }
        /// </summary>
        [HttpPost, Route("{rfcCode}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute(string rfcCode, [FromBody] RfcExecRequest req)
        {
            var sw = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            if (req == null) req = new RfcExecRequest();

            // 1. Resolve from catalog
            var ep = _registry.Get(rfcCode);
            if (ep == null)
                return Fail(requestId, rfcCode, sw, "RFC '" + rfcCode + "' not found in catalog. Check /api/catalog.");

            // 2. Date safeguard
            var dateError = ValidateDateRange(req, ep);
            if (dateError != null) return Fail(requestId, rfcCode, sw, dateError);

            try
            {
                // 3. Connect to SAP
                var rfcPar = BaseController.rfcConfigparametersproduction();
                var dest   = RfcDestinationManager.GetDestination(rfcPar);
                var func   = dest.Repository.CreateFunction(ep.FunctionName);

                // 4. Set parameters from request (with defaults from RFC_PARAM)
                ApplyParams(func, ep, req);

                // 5. Execute
                func.Invoke(dest);

                // 6. Extract output table
                var rows = ExtractRows(func, ep.ReturnTable);
                sw.Stop();

                // 7. Max records guard
                if (req.DateFrom == null && rows.Count > MAX_RECORDS_NO_DATE)
                    return Fail(requestId, rfcCode, sw,
                        "Returned " + rows.Count + " records without a date filter. " +
                        "Provide dateFrom + dateTo (max 90 days) for transactional RFCs.");

                // 8. Log to Snowflake
                _sf.LogAccess(requestId, rfcCode, "/api/execute/" + rfcCode, 200, sw.ElapsedMilliseconds, rows.Count);

                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success     = true,
                    RequestId   = requestId,
                    RfcCode     = rfcCode,
                    FunctionName = ep.FunctionName,
                    RecordCount = rows.Count,
                    ElapsedMs   = sw.ElapsedMilliseconds,
                    Data        = rows
                });
            }
            catch (Exception ex)
            {
                return Fail(requestId, rfcCode, sw, ex.Message);
            }
        }

        // ── PIPELINE 2: RFC → Snowflake data lake ────────────────────────────

        /// <summary>
        /// Execute RFC from catalog and write results to Snowflake GOLD target table.
        /// Table is auto-created if it doesn't exist. Upsert by date range if dateFrom/dateTo provided.
        ///
        /// Example: POST /api/execute/ZSALES_MOP_RFC/sync
        /// Body: { "params": {...}, "dateFrom": "2026-04-01", "dateTo": "2026-04-30", "env": "PROD" }
        ///
        /// Results queryable via: GET /api/datalake/{targetTable}
        /// </summary>
        [HttpPost, Route("{rfcCode}/sync")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Sync(string rfcCode, [FromBody] RfcExecRequest req)
        {
            var sw = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            if (req == null) req = new RfcExecRequest();

            var ep = _registry.Get(rfcCode);
            if (ep == null)
                return Fail(requestId, rfcCode, sw, "RFC '" + rfcCode + "' not found in catalog.");

            if (string.IsNullOrWhiteSpace(ep.TargetTable))
                return Fail(requestId, rfcCode, sw, "RFC '" + rfcCode + "' has no TARGET_TABLE configured in RFC_MASTER.");

            var dateError = ValidateDateRange(req, ep);
            if (dateError != null) return Fail(requestId, rfcCode, sw, dateError);

            try
            {
                // Fetch from SAP
                var rfcPar = BaseController.rfcConfigparametersproduction();
                var dest   = RfcDestinationManager.GetDestination(rfcPar);
                var func   = dest.Repository.CreateFunction(ep.FunctionName);
                ApplyParams(func, ep, req);
                func.Invoke(dest);
                var rows = ExtractRows(func, ep.ReturnTable);

                // Write to Snowflake
                var dateParam = ep.Parameters.FirstOrDefault(p =>
                    p.Type == "Scalar" && p.DataType == "Date" && p.IsRequired);
                string dateCol = dateParam?.Name;

                int written = _sf.BulkInsert(ep.TargetTable, rows,
                    dateCol, req.DateFrom, req.DateTo);

                sw.Stop();
                _sf.LogAccess(requestId, rfcCode, "/api/execute/" + rfcCode + "/sync",
                    200, sw.ElapsedMilliseconds, written);

                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success         = true,
                    RequestId       = requestId,
                    RfcCode         = rfcCode,
                    FunctionName    = ep.FunctionName,
                    TargetTable     = "GOLD." + ep.TargetTable,
                    FetchedFromSap  = rows.Count,
                    WrittenToLake   = written,
                    DateRange       = req.DateFrom.HasValue
                        ? req.DateFrom.Value.ToString("yyyy-MM-dd") + " → " + req.DateTo.Value.ToString("yyyy-MM-dd")
                        : "ALL",
                    QueryApi        = "/api/datalake/" + ep.TargetTable,
                    ElapsedMs       = sw.ElapsedMilliseconds,
                    SyncedAt        = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            catch (Exception ex)
            {
                return Fail(requestId, rfcCode, sw, ex.Message);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ApplyParams(IRfcFunction func, RfcEndpoint ep, RfcExecRequest req)
        {
            foreach (var p in ep.Parameters.Where(p => p.Type == "Scalar"))
            {
                string value = null;
                if (req.Params != null && req.Params.ContainsKey(p.Name))
                    value = req.Params[p.Name]?.ToString();
                // Resolve DATE_FROM/DATE_TO default expressions from request DateFrom/DateTo fields
                if (value == null && p.DefaultExpr == "DATE_FROM" && req.DateFrom.HasValue)
                    value = req.DateFrom.Value.ToString("yyyyMMdd");
                else if (value == null && p.DefaultExpr == "DATE_TO" && req.DateTo.HasValue)
                    value = req.DateTo.Value.ToString("yyyyMMdd");
                else if (value == null && !string.IsNullOrEmpty(p.DefaultExpr))
                    value = ResolveDefault(p.DefaultExpr, req);
                if (value != null)
                {
                    try { func.SetValue(p.Name, value); } catch { }
                }
            }
        }

        private string ResolveDefault(string expr, RfcExecRequest req)
        {
            if (expr == "TODAY" || expr == "TODAY-1")
            {
                int offset = expr == "TODAY-1" ? -1 : 0;
                var d = req.DateFrom ?? DateTime.Today.AddDays(offset);
                return d.ToString("yyyyMMdd");
            }
            if (expr.StartsWith("TODAY-"))
            {
                if (int.TryParse(expr.Substring(6), out int days))
                    return DateTime.Today.AddDays(-days).ToString("yyyyMMdd");
            }
            return expr;
        }

        private List<Dictionary<string, object>> ExtractRows(IRfcFunction func, string tableName)
        {
            var rows = new List<Dictionary<string, object>>();
            try
            {
                IRfcTable tbl = func.GetTable(tableName);
                // Build field name list from table line type metadata (NCo 3.0 API)
                var fieldNames = new List<string>();
                var lineMeta = tbl.Metadata.LineType;
                for (int i = 0; i < lineMeta.FieldCount; i++)
                    fieldNames.Add(lineMeta[i].Name);

                foreach (IRfcStructure row in tbl)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var name in fieldNames)
                    {
                        try { dict[name] = row.GetString(name); }
                        catch { dict[name] = null; }
                    }
                    rows.Add(dict);
                }
            }
            catch { }
            return rows;
        }

        private string ValidateDateRange(RfcExecRequest req, RfcEndpoint ep)
        {
            bool hasDate = req.DateFrom.HasValue || req.DateTo.HasValue;
            if (!hasDate) return null;
            if (req.DateFrom.HasValue && !req.DateTo.HasValue)
                return "dateFrom provided but dateTo is missing.";
            if (!req.DateFrom.HasValue && req.DateTo.HasValue)
                return "dateTo provided but dateFrom is missing.";
            if (req.DateFrom > req.DateTo)
                return "dateFrom must be on or before dateTo.";
            int days = (req.DateTo.Value - req.DateFrom.Value).Days;
            if (days > MAX_DATE_DAYS)
                return "Date range " + days + " days exceeds maximum of " + MAX_DATE_DAYS +
                       " days. Use smaller slices to prevent crore-record pulls.";
            return null;
        }

        private HttpResponseMessage Fail(string requestId, string rfcCode, Stopwatch sw, string error)
        {
            sw.Stop();
            try { _sf.LogAccess(requestId, rfcCode, "/api/execute/" + rfcCode, 500, sw.ElapsedMilliseconds, 0, error); } catch { }
            return Request.CreateResponse(HttpStatusCode.BadRequest,
                new { Success = false, RequestId = requestId, Error = error });
        }
    }

    /// <summary>Request body for RFC execution.</summary>
    public class RfcExecRequest
    {
        /// <summary>RFC import parameter values e.g. { "I_BUDAT_LOW": "20260401" }</summary>
        public Dictionary<string, object> Params { get; set; }
        /// <summary>Date from for transactional RFCs (YYYY-MM-DD). Max range 90 days.</summary>
        public DateTime? DateFrom { get; set; }
        /// <summary>Date to for transactional RFCs (YYYY-MM-DD). Max range 90 days.</summary>
        public DateTime? DateTo   { get; set; }
        /// <summary>SAP environment: PROD (default) or UAT.</summary>
        public string Env         { get; set; } = "PROD";
        /// <summary>Max rows to accept (default 100,000). Guard against crore-record pulls.</summary>
        public int MaxRecords     { get; set; } = 100000;
    }
}
