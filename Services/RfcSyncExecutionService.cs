using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SAP.Middleware.Connector;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// Core RFC → Snowflake sync logic extracted into a reusable service.
    /// Used by both SyncJobController (per-request) and DataSyncScheduler (background timer).
    ///
    /// Key improvements over developer's RfcEngine:
    ///   - Date range validation (90-day max from our implementation)
    ///   - Fallback SAP connection (SapConnectionService → BaseController)
    ///   - Access logging to GOLD.RFC_API_ACCESS_LOG
    ///   - No hardcoded credentials
    /// </summary>
    public class RfcSyncExecutionService
    {
        private readonly EndpointRegistryService _registry = EndpointRegistryService.Instance;
        private readonly SapConnectionService    _sapConn  = SapConnectionService.Instance;
        private readonly SnowflakeService        _sf       = new SnowflakeService();

        public const int MAX_DATE_DAYS       = 90;
        public const int MAX_RECORDS_NO_DATE = 100000;

        public class ExecResult
        {
            public bool   Success      { get; set; }
            public string RequestId    { get; set; }
            public string RfcCode      { get; set; }
            public string FunctionName { get; set; }
            public int    FetchedRows  { get; set; }
            public int    WrittenRows  { get; set; }
            public string TargetTable  { get; set; }
            public long   ElapsedMs    { get; set; }
            public string DateRange    { get; set; }
            public string Error        { get; set; }
            public List<Dictionary<string, object>> Rows { get; set; }
        }

        /// <summary>Execute RFC and return raw JSON rows (Pipeline 1).</summary>
        public ExecResult Fetch(string rfcCode, Dictionary<string, object> userParams = null,
            DateTime? dateFrom = null, DateTime? dateTo = null, string env = "PROD")
        {
            var res = Core(rfcCode, userParams, dateFrom, dateTo, env, writeToLake: false);
            _sf.LogAccess(res.RequestId, rfcCode, "/api/execute/" + rfcCode,
                res.Success ? 200 : 500, res.ElapsedMs, res.FetchedRows, res.Error);
            return res;
        }

        /// <summary>Execute RFC and write to Snowflake GOLD target table (Pipeline 2).</summary>
        public ExecResult Sync(string rfcCode, Dictionary<string, object> userParams = null,
            DateTime? dateFrom = null, DateTime? dateTo = null, string env = "PROD")
        {
            var res = Core(rfcCode, userParams, dateFrom, dateTo, env, writeToLake: true);
            _sf.LogAccess(res.RequestId, rfcCode, "/api/execute/" + rfcCode + "/sync",
                res.Success ? 200 : 500, res.ElapsedMs, res.WrittenRows, res.Error);
            return res;
        }

        private ExecResult Core(string rfcCode, Dictionary<string, object> userParams,
            DateTime? dateFrom, DateTime? dateTo, string env, bool writeToLake)
        {
            var sw = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
            var res = new ExecResult { RequestId = requestId, RfcCode = rfcCode };

            // Date range validation
            if (dateFrom.HasValue || dateTo.HasValue)
            {
                if (!dateFrom.HasValue || !dateTo.HasValue)
                { res.Error = "Both dateFrom and dateTo required when either is provided."; return res; }
                int days = (dateTo.Value - dateFrom.Value).Days;
                if (days < 0) { res.Error = "dateFrom must be on or before dateTo."; return res; }
                if (days > MAX_DATE_DAYS)
                { res.Error = $"Date range {days} days exceeds {MAX_DATE_DAYS}-day limit. Use smaller slices."; return res; }
            }

            // Resolve RFC from catalog
            var ep = _registry.Get(rfcCode);
            if (ep == null)
            { res.Error = $"RFC '{rfcCode}' not found in RFC_MASTER. Refresh catalog: POST /api/catalog/refresh"; return res; }

            if (writeToLake && string.IsNullOrWhiteSpace(ep.TargetTable))
            { res.Error = $"RFC '{rfcCode}' has no TARGET_TABLE in RFC_MASTER. Cannot sync."; return res; }

            res.FunctionName = ep.FunctionName;
            res.TargetTable  = ep.TargetTable;
            res.DateRange    = dateFrom.HasValue
                ? dateFrom.Value.ToString("yyyy-MM-dd") + " -> " + dateTo.Value.ToString("yyyy-MM-dd") : "ALL";

            try
            {
                // SAP connection: try DB-driven first, fall back to BaseController
                var rfcPar = _sapConn.GetConfig(env, ep.ConnId)
                             ?? BaseController.rfcConfigparametersproduction();

                var dest = RfcDestinationManager.GetDestination(rfcPar);
                var func = dest.Repository.CreateFunction(ep.FunctionName);

                ApplyParams(func, ep, userParams, dateFrom, dateTo);
                func.Invoke(dest);

                var rows = ExtractRows(func, ep.ReturnTable);
                res.FetchedRows = rows.Count;
                res.Rows = rows;

                // Max records guard
                if (!dateFrom.HasValue && rows.Count > MAX_RECORDS_NO_DATE)
                {
                    res.Error = $"RFC returned {rows.Count} records without date filter. Add dateFrom+dateTo.";
                    return res;
                }

                if (writeToLake && rows.Count > 0)
                {
                    var dateParam = ep.Parameters.FirstOrDefault(p =>
                        p.Type == "Scalar" && (p.DataType == "Date" || p.DataType == "DATS"));
                    res.WrittenRows = _sf.BulkInsert(ep.TargetTable, rows, dateParam?.Name, dateFrom, dateTo);
                }

                sw.Stop();
                res.ElapsedMs = sw.ElapsedMilliseconds;
                res.Success = true;
                return res;
            }
            catch (Exception ex)
            {
                sw.Stop();
                res.ElapsedMs = sw.ElapsedMilliseconds;
                res.Error = ex.Message;
                return res;
            }
        }

        private void ApplyParams(IRfcFunction func, RfcEndpoint ep,
            Dictionary<string, object> userParams, DateTime? dateFrom, DateTime? dateTo)
        {
            foreach (var p in ep.Parameters.Where(x => x.Type == "Scalar"))
            {
                string val = null;
                if (userParams != null && userParams.ContainsKey(p.Name))
                    val = userParams[p.Name]?.ToString();
                if (val == null && !string.IsNullOrEmpty(p.DefaultExpr))
                    val = ResolveDefault(p.DefaultExpr, dateFrom, dateTo);
                if (val != null) try { func.SetValue(p.Name, val); } catch { }
            }
        }

        private string ResolveDefault(string expr, DateTime? df, DateTime? dt)
        {
            if (expr == "TODAY")      return DateTime.Today.ToString("yyyyMMdd");
            if (expr == "TODAY-1")    return DateTime.Today.AddDays(-1).ToString("yyyyMMdd");
            if (expr == "DATE_FROM" && df.HasValue) return df.Value.ToString("yyyyMMdd");
            if (expr == "DATE_TO"   && dt.HasValue) return dt.Value.ToString("yyyyMMdd");
            if (expr.StartsWith("TODAY-") && int.TryParse(expr.Substring(6), out int d))
                return DateTime.Today.AddDays(-d).ToString("yyyyMMdd");
            return expr;
        }

        internal List<Dictionary<string, object>> ExtractRows(IRfcFunction func, string tableName)
        {
            var rows = new List<Dictionary<string, object>>();
            try
            {
                var tbl = func.GetTable(tableName);
                var lineMeta = tbl.Metadata.LineType;
                var fields = new List<string>();
                for (int i = 0; i < lineMeta.FieldCount; i++) fields.Add(lineMeta[i].Name);
                foreach (IRfcStructure row in tbl)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var f in fields) try { dict[f] = row.GetString(f); } catch { dict[f] = null; }
                    rows.Add(dict);
                }
            }
            catch { }
            return rows;
        }
    }
}
