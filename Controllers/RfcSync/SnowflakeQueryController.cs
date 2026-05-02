using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_SRM_Routing_Application.Services;
using Vendor_Application_MVC.Controllers;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Snowflake Data Lake Query API — reads from Snowflake GOLD schema.
    /// This is the READ side of Pipeline 2 (RFC → Snowflake → Apps).
    ///
    /// Any table in Snowflake GOLD is queryable here with date filtering and pagination.
    /// No configuration needed — tables appear automatically after sync.
    ///
    /// GET /api/datalake/{tableName}                              → all records (up to limit)
    /// GET /api/datalake/{tableName}?fromDate=2026-04-01&amp;toDate=2026-04-30  → date-scoped
    /// GET /api/datalake/{tableName}?filter=REGIO:06             → column filter
    /// GET /api/datalake/{tableName}/schema                      → column definitions
    /// GET /api/datalake/tables                                  → list all GOLD tables
    /// </summary>
    [RoutePrefix("api/datalake")]
    public class SnowflakeQueryController : BaseController
    {
        private readonly SnowflakeService _sf = new SnowflakeService();
        private static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9_]{1,200}$");
        private static readonly Regex SafeVal  = new Regex(@"^[A-Za-z0-9 _\-\.\:]{0,200}$");
        private const int HARD_LIMIT = 50000;
        private const int MAX_DATE_DAYS = 90;

        // ── List all tables ───────────────────────────────────────────────────
        /// <summary>List all tables in Snowflake GOLD schema available for query.</summary>
        [HttpGet, Route("tables")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Tables(string search = null)
        {
            try
            {
                string sql = @"SELECT TABLE_NAME, ROW_COUNT, BYTES,
                                      LAST_ALTERED
                               FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_SCHEMA = 'GOLD' AND TABLE_TYPE = 'BASE TABLE'
                               ORDER BY TABLE_NAME";
                var rows = _sf.QueryAsList(sql);
                if (!string.IsNullOrWhiteSpace(search))
                    rows = rows.FindAll(r => (r["TABLE_NAME"]?.ToString() ?? "")
                                            .IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success = true,
                    Schema  = "GOLD",
                    Count   = rows.Count,
                    Tables  = rows
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── Schema ────────────────────────────────────────────────────────────
        /// <summary>Get column definitions for any Snowflake GOLD table.</summary>
        [HttpGet, Route("{tableName}/schema")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Schema(string tableName)
        {
            if (!SafeName.IsMatch(tableName))
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Success = false, Error = "Invalid table name." });
            try
            {
                var cols = _sf.QueryAsList(
                    @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH,
                             NUMERIC_PRECISION, ORDINAL_POSITION
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = 'GOLD' AND TABLE_NAME = :tbl
                      ORDER BY ORDINAL_POSITION",
                    new Dictionary<string, object> { { "tbl", tableName.ToUpper() } });

                if (cols.Count == 0)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new { Success = false, Error = "Table '" + tableName + "' not found in GOLD schema." });

                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success   = true,
                    TableName = tableName.ToUpper(),
                    Schema    = "GOLD",
                    Columns   = cols
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── Query ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Query any Snowflake GOLD table with optional date range and column filter.
        /// fromDate/toDate: scoped pull (max 90 days). filter=COLUMN:VALUE for equality filter.
        /// top: max rows returned (default 5000, hard limit 50000).
        /// </summary>
        [HttpGet, Route("{tableName}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Query(
            string tableName,
            string fromDate   = null,
            string toDate     = null,
            string dateColumn = null,
            string filter     = null,
            int    top        = 5000)
        {
            if (!SafeName.IsMatch(tableName))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Success = false, Error = "Invalid table name." });

            // Date range validation
            DateTime? dtFrom = null, dtTo = null;
            if (!string.IsNullOrWhiteSpace(fromDate) || !string.IsNullOrWhiteSpace(toDate))
            {
                if (!DateTime.TryParse(fromDate, out var f) || !DateTime.TryParse(toDate, out var t))
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Success = false, Error = "fromDate/toDate must be YYYY-MM-DD." });
                if (f > t)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Success = false, Error = "fromDate must be <= toDate." });
                int span = (t - f).Days;
                if (span > MAX_DATE_DAYS)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Success = false, Error = "Date range " + span + " days exceeds 90-day limit." });
                dtFrom = f; dtTo = t;
            }

            try
            {
                int limit    = Math.Min(Math.Max(top, 1), HARD_LIMIT);
                string safeT = SnowflakeService.SanitizeIdentifier(tableName);
                string where = "1=1";
                var parms    = new Dictionary<string, object>();

                // Date filter
                if (dtFrom.HasValue && !string.IsNullOrWhiteSpace(dateColumn))
                {
                    if (!SafeName.IsMatch(dateColumn))
                        return Request.CreateResponse(HttpStatusCode.BadRequest,
                            new { Success = false, Error = "Invalid dateColumn." });
                    string safeCol = SnowflakeService.SanitizeIdentifier(dateColumn);
                    where += " AND " + safeCol + " >= :dfrom AND " + safeCol + " <= :dto";
                    parms["dfrom"] = dtFrom.Value.ToString("yyyy-MM-dd");
                    parms["dto"]   = dtTo.Value.ToString("yyyy-MM-dd");
                }

                // Column filter
                if (!string.IsNullOrWhiteSpace(filter) && filter.Contains(":"))
                {
                    var parts = filter.Split(new[] { ':' }, 2);
                    if (parts.Length == 2 && SafeName.IsMatch(parts[0]) && SafeVal.IsMatch(parts[1]))
                    {
                        string safeCol = SnowflakeService.SanitizeIdentifier(parts[0]);
                        where += " AND " + safeCol + " = :fval";
                        parms["fval"] = parts[1];
                    }
                }

                string sql = "SELECT * FROM GOLD." + safeT + " WHERE " + where +
                             " LIMIT " + limit;
                var rows = _sf.QueryAsList(sql, parms);

                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success   = true,
                    TableName = safeT,
                    Schema    = "GOLD",
                    Count     = rows.Count,
                    DateRange = dtFrom.HasValue
                        ? dtFrom.Value.ToString("yyyy-MM-dd") + " → " + dtTo.Value.ToString("yyyy-MM-dd") : null,
                    Filter    = filter,
                    Data      = rows
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }
    }
}
