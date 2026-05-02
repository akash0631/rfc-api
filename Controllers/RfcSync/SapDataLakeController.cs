using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// SAP Data Lake API — generic dynamic-schema RFC → SQL → REST pipeline
    /// POST /api/SapDataLake/Sync    — upsert any JSON records into HUCreation SQL
    /// GET  /api/SapDataLake/Query/{tableName}?top=N&filter=COL:VAL
    ///                               — read any table back as JSON
    /// GET  /api/SapDataLake/Schema/{tableName} — column list
    /// GET  /api/SapDataLake/Tables  — list all SAP data lake tables
    /// </summary>
    [RoutePrefix("api/SapDataLake")]
    public class SapDataLakeController : BaseController
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

        private static readonly Regex SafeName  = new Regex(@"^[A-Za-z0-9_]{1,128}$");
        private static readonly Regex SafeValue = new Regex(@"^[A-Za-z0-9 _\-\.]{0,256}$");

        // ── SYNC ─────────────────────────────────────────────────────────────
        // POST /api/SapDataLake/Sync
        // Body: { "tableName": "SAP_PLANT_MASTER", "keyColumn": "WERKS",
        //         "records": [ { "WERKS":"HA01", "NAME1":"Store", ... } ] }
        [HttpPost]
        [Route("Sync")]
        public HttpResponseMessage Sync([FromBody] SapDataLakeSyncRequest req)
        {
            if (req == null)
                return Fail("Request body is required.");
            if (string.IsNullOrWhiteSpace(req.TableName) || !SafeName.IsMatch(req.TableName))
                return Fail("TableName is required and must be alphanumeric/underscore.");
            if (req.Records == null || req.Records.Count == 0)
                return Fail("Records array is required and must not be empty.");
            if (string.IsNullOrWhiteSpace(req.KeyColumn) || !SafeName.IsMatch(req.KeyColumn))
                return Fail("KeyColumn is required.");

            // Derive columns from the union of all record keys
            var allKeys = req.Records.SelectMany(r => r.Keys).Distinct().OrderBy(k => k).ToList();
            if (!allKeys.All(k => SafeName.IsMatch(k)))
                return Fail("Record keys must be alphanumeric/underscore only.");
            if (!allKeys.Contains(req.KeyColumn, StringComparer.OrdinalIgnoreCase))
                return Fail($"KeyColumn '{req.KeyColumn}' not found in records.");

            int created   = 0;
            int upserted  = 0;
            bool existed  = false;

            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();

                    // 1. Create table if not exists
                    existed = TableExists(conn, req.TableName);
                    if (!existed)
                    {
                        CreateDynamicTable(conn, req.TableName, allKeys, req.KeyColumn);
                        created = 1;
                    }
                    else
                    {
                        // Add any new columns that don't exist yet (schema evolution)
                        var existingCols = GetColumns(conn, req.TableName);
                        foreach (var col in allKeys.Except(existingCols, StringComparer.OrdinalIgnoreCase))
                            AlterAddColumn(conn, req.TableName, col);
                    }

                    // 2. Upsert all records in a single transaction
                    upserted = UpsertRecords(conn, req.TableName, req.KeyColumn, allKeys, req.Records);
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Success         = true,
                    TableName       = req.TableName,
                    TableCreated    = created > 0,
                    TableExisted    = existed,
                    Columns         = allKeys.Count,
                    RecordsUpserted = upserted,
                    SyncedAt        = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── QUERY ─────────────────────────────────────────────────────────────
        // GET /api/SapDataLake/Query/{tableName}?top=1000&filter=LAND1:IN
        [HttpGet]
        [Route("Query/{tableName}")]
        public HttpResponseMessage Query(string tableName, int top = 5000, string filter = null)
        {
            if (!SafeName.IsMatch(tableName))
                return Fail("Invalid table name.");

            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    if (!TableExists(conn, tableName))
                        return Fail($"Table '{tableName}' does not exist.", HttpStatusCode.NotFound);

                    // Build safe parameterised WHERE clause from filter=COL:VAL
                    string where = "1=1";
                    SqlParameter filterParam = null;
                    if (!string.IsNullOrWhiteSpace(filter) && filter.Contains(":"))
                    {
                        var parts = filter.Split(new[] { ':' }, 2);
                        if (SafeName.IsMatch(parts[0]) && SafeValue.IsMatch(parts[1]))
                        {
                            where = $"[{parts[0]}] = @FilterVal";
                            filterParam = new SqlParameter("@FilterVal", parts[1]);
                        }
                    }

                    int limit = Math.Min(Math.Max(top, 1), 50000);
                    string sql = $"SELECT TOP {limit} * FROM [dbo].[{tableName}] WHERE {where} ORDER BY (SELECT NULL)";

                    var rows  = new List<Dictionary<string, object>>();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (filterParam != null) cmd.Parameters.Add(filterParam);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            var cols = Enumerable.Range(0, rdr.FieldCount)
                                                 .Select(i => rdr.GetName(i)).ToList();
                            while (rdr.Read())
                            {
                                var row = new Dictionary<string, object>();
                                foreach (var col in cols)
                                    row[col] = rdr.IsDBNull(rdr.GetOrdinal(col))
                                               ? null : rdr.GetValue(rdr.GetOrdinal(col));
                                rows.Add(row);
                            }
                        }
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        Success   = true,
                        TableName = tableName,
                        Count     = rows.Count,
                        Filter    = filter,
                        Data      = rows
                    });
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── SCHEMA ────────────────────────────────────────────────────────────
        [HttpGet]
        [Route("Schema/{tableName}")]
        public HttpResponseMessage Schema(string tableName)
        {
            if (!SafeName.IsMatch(tableName))
                return Fail("Invalid table name.");
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    if (!TableExists(conn, tableName)) return Fail("Table not found.", HttpStatusCode.NotFound);
                    var cols = GetColumnsWithTypes(conn, tableName);
                    return Request.CreateResponse(HttpStatusCode.OK,
                        new { Success = true, TableName = tableName, Columns = cols });
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── TABLES LIST ───────────────────────────────────────────────────────
        [HttpGet]
        [Route("Tables")]
        public HttpResponseMessage Tables(string search = null)
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    string sql = "SELECT TABLE_NAME, " +
                                 "(SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_NAME = t.TABLE_NAME) AS ColumnCount " +
                                 "FROM INFORMATION_SCHEMA.TABLES t WHERE TABLE_TYPE='BASE TABLE'" +
                                 (string.IsNullOrWhiteSpace(search) ? "" : " AND TABLE_NAME LIKE '%' + @Search + '%'") +
                                 " ORDER BY TABLE_NAME";
                    var tables = new List<object>();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(search))
                            cmd.Parameters.AddWithValue("@Search", search);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read())
                                tables.Add(new { TableName = rdr.GetString(0), ColumnCount = rdr.GetInt32(1) });
                    }
                    return Request.CreateResponse(HttpStatusCode.OK,
                        new { Success = true, Count = tables.Count, Tables = tables });
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── SQL Helpers ───────────────────────────────────────────────────────
        private bool TableExists(SqlConnection conn, string table)
        {
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@T", conn))
            {
                cmd.Parameters.AddWithValue("@T", table);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        private void CreateDynamicTable(SqlConnection conn, string table, List<string> cols, string key)
        {
            var colDefs = cols.Select(c =>
                $"[{c}] NVARCHAR(500) NULL").ToList();
            colDefs.Add("[_SYNCED_AT] DATETIME DEFAULT GETDATE()");

            string sql =
                $"CREATE TABLE [dbo].[{table}] (" +
                string.Join(", ", colDefs) +
                $", CONSTRAINT [PK_{table}] PRIMARY KEY ([{key}]))";

            using (var cmd = new SqlCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        private void AlterAddColumn(SqlConnection conn, string table, string col)
        {
            string sql = $"ALTER TABLE [dbo].[{table}] ADD [{col}] NVARCHAR(500) NULL";
            using (var cmd = new SqlCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        private List<string> GetColumns(SqlConnection conn, string table)
        {
            var cols = new List<string>();
            using (var cmd = new SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@T", conn))
            {
                cmd.Parameters.AddWithValue("@T", table);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read()) cols.Add(rdr.GetString(0));
            }
            return cols;
        }

        private List<object> GetColumnsWithTypes(SqlConnection conn, string table)
        {
            var cols = new List<object>();
            using (var cmd = new SqlCommand(
                "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@T ORDER BY ORDINAL_POSITION", conn))
            {
                cmd.Parameters.AddWithValue("@T", table);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        cols.Add(new {
                            Name      = rdr.GetString(0),
                            Type      = rdr.GetString(1),
                            MaxLength = rdr.IsDBNull(2) ? (int?)null : rdr.GetInt32(2)
                        });
            }
            return cols;
        }

        private int UpsertRecords(SqlConnection conn, string table, string key,
                                   List<string> cols, List<Dictionary<string, string>> records)
        {
            // MERGE (upsert) using a temp table for bulk performance
            string tmpTable  = $"#tmp_{table}_{Guid.NewGuid():N}".Substring(0, 40);
            string colList   = string.Join(", ", cols.Select(c => $"[{c}]"));
            string paramList = string.Join(", ", cols.Select(c => $"@{c}"));
            string updateSet = string.Join(", ", cols.Where(c => !c.Equals(key, StringComparison.OrdinalIgnoreCase))
                                                      .Select(c => $"T.[{c}] = S.[{c}]"));

            // Create temp table matching main table columns
            string createTmp = $"SELECT TOP 0 {colList} INTO {tmpTable} FROM [dbo].[{table}]";

            int count = 0;
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = new SqlCommand(createTmp, conn, tx))
                        cmd.ExecuteNonQuery();

                    // Bulk insert into temp
                    string insertTmp = $"INSERT INTO {tmpTable} ({colList}) VALUES ({paramList})";
                    foreach (var row in records)
                    {
                        using (var cmd = new SqlCommand(insertTmp, conn, tx))
                        {
                            foreach (var col in cols)
                            {
                                string val = row.TryGetValue(col, out var v) ? v : null;
                                cmd.Parameters.Add(new SqlParameter($"@{col}", SqlDbType.NVarChar, 500)
                                    { Value = (object)val ?? DBNull.Value });
                            }
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // MERGE into main table
                    string merge =
                        $"MERGE [dbo].[{table}] AS T " +
                        $"USING {tmpTable} AS S ON T.[{key}] = S.[{key}] " +
                        $"WHEN MATCHED THEN UPDATE SET {updateSet}, T.[_SYNCED_AT]=GETDATE() " +
                        $"WHEN NOT MATCHED THEN INSERT ({colList}, [_SYNCED_AT]) VALUES ({paramList.Replace("@", "S.[").Replace(", S.[", "], S.[")
                            .Replace(string.Join(", ", cols.Select(c => $"S.[{c}")), string.Join(", ", cols.Select(c => $"S.[{c}]")))}, GETDATE());";

                    // Simpler equivalent that avoids MERGE parameter complexity:
                    string mergeSql =
                        $"MERGE [dbo].[{table}] WITH (HOLDLOCK) AS T " +
                        $"USING {tmpTable} AS S ON T.[{key}] = S.[{key}] " +
                        $"WHEN MATCHED THEN UPDATE SET " +
                        string.Join(", ", cols.Where(c => !c.Equals(key, StringComparison.OrdinalIgnoreCase))
                                              .Select(c => $"T.[{c}]=S.[{c}]")) +
                        $", T.[_SYNCED_AT]=GETDATE() " +
                        $"WHEN NOT MATCHED THEN INSERT ({colList},[_SYNCED_AT]) " +
                        $"VALUES ({string.Join(",", cols.Select(c => $"S.[{c}]"))},GETDATE());";

                    using (var cmd = new SqlCommand(mergeSql, conn, tx))
                        count = cmd.ExecuteNonQuery();

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
            return count;
        }

        private HttpResponseMessage Fail(string msg, HttpStatusCode code = HttpStatusCode.BadRequest)
            => Request.CreateResponse(code, new { Success = false, Error = msg });
    }

    // ── Request model ─────────────────────────────────────────────────────────
    public class SapDataLakeSyncRequest
    {
        public string TableName { get; set; }
        public string KeyColumn { get; set; }
        public List<Dictionary<string, string>> Records { get; set; }
    }
}
