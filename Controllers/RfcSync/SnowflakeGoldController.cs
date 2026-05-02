using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_SRM_Routing_Application.Services;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Snowflake GOLD Direct Access Layer — enhanced OData-style query on any GOLD table.
    /// Based on developer's SnowflakeDAB but integrated into our IIS infrastructure (no extra project).
    ///
    /// Features from developer's implementation:
    ///   - Pagination ($top, $skip)
    ///   - Multi-column filter ($filter=COL:VALUE,COL2:VALUE2)
    ///   - Order by ($orderby=COL desc)
    ///   - Column selection ($select=COL1,COL2)
    ///   - Schema discovery (column types, nullable, precision)
    ///   - Table listing with row counts
    ///   - Write support (INSERT, UPDATE, DELETE)
    ///
    /// GET  /api/gold                              → list all GOLD tables with stats
    /// GET  /api/gold/{table}                      → query with pagination/filter/sort
    /// GET  /api/gold/{table}/schema               → column definitions
    /// POST /api/gold/{table}                      → insert record
    /// PUT  /api/gold/{table}/{pk}                 → update by primary key
    /// DELETE /api/gold/{table}/{pk}               → delete by primary key
    /// </summary>
    [RoutePrefix("api/gold")]
    public class SnowflakeGoldController : BaseController
    {
        private readonly SnowflakeService _sf = new SnowflakeService();
        private static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9_]{1,200}$");
        private static readonly Regex SafeVal  = new Regex(@"^[A-Za-z0-9 _\-\.\/\:\,]{0,500}$");
        private const int DEFAULT_TOP  = 1000;
        private const int HARD_LIMIT   = 50000;
        private const int MAX_DATE_DAYS = 90;

        // ── Table listing ─────────────────────────────────────────────────────
        /// <summary>List all tables in Snowflake GOLD schema with row counts and byte sizes.</summary>
        [HttpGet, Route("")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Tables(string search = null)
        {
            try
            {
                var rows = _sf.QueryAsList(@"
                    SELECT TABLE_NAME, ROW_COUNT, BYTES,
                           ROUND(BYTES/1024/1024, 2) AS SIZE_MB, LAST_ALTERED
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = 'GOLD' AND TABLE_TYPE = 'BASE TABLE'
                    ORDER BY TABLE_NAME");
                if (!string.IsNullOrWhiteSpace(search))
                    rows = rows.FindAll(r =>
                        (r["TABLE_NAME"]?.ToString() ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                return Ok(new { Success = true, Schema = "GOLD", Count = rows.Count, Tables = rows });
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Schema discovery ──────────────────────────────────────────────────
        /// <summary>Get column definitions, types, and constraints for any GOLD table.</summary>
        [HttpGet, Route("{table}/schema")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Schema(string table)
        {
            if (!SafeName.IsMatch(table)) return BadReq("Invalid table name.");
            try
            {
                var cols = _sf.QueryAsList(@"
                    SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE,
                           CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE,
                           COLUMN_DEFAULT, ORDINAL_POSITION
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = 'GOLD' AND TABLE_NAME = :tbl
                    ORDER BY ORDINAL_POSITION",
                    new Dictionary<string, object> { { "tbl", table.ToUpper() } });
                if (cols.Count == 0)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new { Success = false, Error = $"Table '{table}' not found in GOLD schema." });
                return Ok(new { Success = true, Table = table.ToUpper(), Schema = "GOLD", Columns = cols });
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Query / List ──────────────────────────────────────────────────────
        /// <summary>
        /// Query any GOLD table with OData-style params.
        /// $filter=COL:VALUE,COL2:VALUE2  (multi-column AND filter)
        /// $orderby=COL desc
        /// $select=COL1,COL2
        /// $top=1000 (max 50000)
        /// $skip=0
        /// fromDate/toDate + dateColumn for date range scoping
        /// </summary>
        [HttpGet, Route("{table}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Query(
            string table,
            string filter    = null,
            string orderby   = null,
            string select_   = null,
            int    top       = DEFAULT_TOP,
            int    skip      = 0,
            string fromDate  = null,
            string toDate    = null,
            string dateColumn = null)
        {
            if (!SafeName.IsMatch(table)) return BadReq("Invalid table name.");

            int limit = Math.Min(Math.Max(top, 1), HARD_LIMIT);
            string safeT = SnowflakeService.SanitizeIdentifier(table);
            var where  = new List<string>();
            var parms  = new Dictionary<string, object>();

            // Date range filter
            if (!string.IsNullOrWhiteSpace(fromDate) || !string.IsNullOrWhiteSpace(toDate))
            {
                if (!DateTime.TryParse(fromDate, out var df) || !DateTime.TryParse(toDate, out var dt))
                    return BadReq("fromDate/toDate must be YYYY-MM-DD.");
                if (df > dt) return BadReq("fromDate must be <= toDate.");
                int span = (dt - df).Days;
                if (span > MAX_DATE_DAYS) return BadReq($"Date range {span} days exceeds {MAX_DATE_DAYS}-day limit.");
                string dcol = !string.IsNullOrWhiteSpace(dateColumn) ? dateColumn : "POSTING_DATE";
                if (!SafeName.IsMatch(dcol)) return BadReq("Invalid dateColumn.");
                string sdcol = SnowflakeService.SanitizeIdentifier(dcol);
                where.Add($"{sdcol} >= :dfrom AND {sdcol} <= :dto");
                parms["dfrom"] = df.ToString("yyyy-MM-dd");
                parms["dto"]   = dt.ToString("yyyy-MM-dd");
            }

            // Multi-column filter: COL1:VAL1,COL2:VAL2
            if (!string.IsNullOrWhiteSpace(filter))
            {
                int pIdx = 0;
                foreach (var part in filter.Split(','))
                {
                    var kv = part.Split(new[] { ':' }, 2);
                    if (kv.Length != 2) continue;
                    string col = kv[0].Trim(), val = kv[1].Trim();
                    if (!SafeName.IsMatch(col) || !SafeVal.IsMatch(val)) continue;
                    string safeC = SnowflakeService.SanitizeIdentifier(col);
                    string pname = "fp" + pIdx++;
                    where.Add($"{safeC} = :{pname}");
                    parms[pname] = val;
                }
            }

            // Column selection
            string selectClause = "*";
            if (!string.IsNullOrWhiteSpace(select_))
            {
                var cols = new List<string>();
                foreach (var c in select_.Split(','))
                {
                    string col = c.Trim();
                    if (SafeName.IsMatch(col)) cols.Add(SnowflakeService.SanitizeIdentifier(col));
                }
                if (cols.Count > 0) selectClause = string.Join(", ", cols);
            }

            // Order by
            string orderClause = "";
            if (!string.IsNullOrWhiteSpace(orderby))
            {
                var parts = orderby.Trim().Split(new[] { ' ' }, 2);
                string col = parts[0].Trim();
                string dir = parts.Length > 1 && parts[1].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (SafeName.IsMatch(col)) orderClause = $" ORDER BY {SnowflakeService.SanitizeIdentifier(col)} {dir}";
            }

            string whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";
            string sql = $"SELECT {selectClause} FROM GOLD.{safeT}{whereClause}{orderClause} LIMIT {limit}";
            if (skip > 0) sql += $" OFFSET {skip}";

            try
            {
                var rows = _sf.QueryAsList(sql, parms);
                return Ok(new {
                    Success = true, Table = safeT, Schema = "GOLD",
                    Count = rows.Count, Top = limit, Skip = skip,
                    Filter = filter, OrderBy = orderby,
                    Data = rows
                });
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Write operations ──────────────────────────────────────────────────
        /// <summary>Insert a record into any writable GOLD table.</summary>
        [HttpPost, Route("{table}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Insert(string table, [FromBody] Dictionary<string, object> record)
        {
            if (!SafeName.IsMatch(table)) return BadReq("Invalid table name.");
            if (record == null || record.Count == 0) return BadReq("Request body required.");
            try
            {
                string safeT = SnowflakeService.SanitizeIdentifier(table);
                var cols = new List<string>(); var pNames = new List<string>();
                var parms = new Dictionary<string, object>();
                int i = 0;
                foreach (var kv in record)
                {
                    if (!SafeName.IsMatch(kv.Key)) continue;
                    cols.Add(SnowflakeService.SanitizeIdentifier(kv.Key));
                    string pn = "iv" + i++;
                    pNames.Add(":" + pn);
                    parms[pn] = kv.Value ?? DBNull.Value;
                }
                string sql = $"INSERT INTO GOLD.{safeT} ({string.Join(",", cols)}) VALUES ({string.Join(",", pNames)})";
                _sf.ExecuteNonQuery(sql, parms);
                return Ok(new { Success = true, Message = $"Record inserted into GOLD.{safeT}." });
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        /// <summary>Update a record by primary key (ID column by default).</summary>
        [HttpPut, Route("{table}/{pkValue}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Update(string table, string pkValue,
            [FromBody] Dictionary<string, object> record, string pkCol = "ID")
        {
            if (!SafeName.IsMatch(table) || !SafeName.IsMatch(pkCol)) return BadReq("Invalid table/pk name.");
            if (record == null || record.Count == 0) return BadReq("Request body required.");
            try
            {
                string safeT = SnowflakeService.SanitizeIdentifier(table);
                string safePk = SnowflakeService.SanitizeIdentifier(pkCol);
                var sets = new List<string>();
                var parms = new Dictionary<string, object>();
                int i = 0;
                foreach (var kv in record)
                {
                    if (!SafeName.IsMatch(kv.Key)) continue;
                    string pn = "uv" + i++;
                    sets.Add($"{SnowflakeService.SanitizeIdentifier(kv.Key)} = :{pn}");
                    parms[pn] = kv.Value ?? DBNull.Value;
                }
                parms["pkv"] = pkValue;
                string sql = $"UPDATE GOLD.{safeT} SET {string.Join(", ", sets)} WHERE {safePk} = :pkv";
                int rows = _sf.ExecuteNonQuery(sql, parms);
                return Ok(new { Success = true, RowsAffected = rows });
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        /// <summary>Delete a record by primary key.</summary>
        [HttpDelete, Route("{table}/{pkValue}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Delete(string table, string pkValue, string pkCol = "ID")
        {
            if (!SafeName.IsMatch(table) || !SafeName.IsMatch(pkCol)) return BadReq("Invalid table/pk name.");
            try
            {
                string safeT  = SnowflakeService.SanitizeIdentifier(table);
                string safePk = SnowflakeService.SanitizeIdentifier(pkCol);
                int rows = _sf.ExecuteNonQuery(
                    $"DELETE FROM GOLD.{safeT} WHERE {safePk} = :pkv",
                    new Dictionary<string, object> { { "pkv", pkValue } });
                return Ok(new { Success = true, RowsDeleted = rows });
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        private HttpResponseMessage Ok(object body)   => Request.CreateResponse(HttpStatusCode.OK, body);
        private HttpResponseMessage BadReq(string msg)=> Request.CreateResponse(HttpStatusCode.BadRequest, new { Success=false, Error=msg });
        private HttpResponseMessage Err(string msg)   => Request.CreateResponse(HttpStatusCode.InternalServerError, new { Success=false, Error=msg });
    }
}
