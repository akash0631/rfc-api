using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Snowflake.Data.Client;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// Snowflake connectivity service for Snowflake.Data .NET driver 2.1.5.
    ///
    /// KEY COMPATIBILITY FIX:
    ///   Snowflake.Data 2.1.5 throws "No corresponding Snowflake type for type AnsiString"
    ///   when parameter DbType is inferred from a boxed .NET value — including NULL values
    ///   where DbType is never set and defaults to AnsiString.
    ///   Fix: always set DbType.String before assigning Value, for every parameter.
    ///   Snowflake handles implicit type coercion (int, date, boolean, null all work from string).
    ///
    /// Connection: V2RETAIL.GOLD via account iafphkw-hh80816 (Azure Central India).
    /// </summary>
    public class SnowflakeService
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["Snowflake"]?.ConnectionString
            ?? "account=iafphkw-hh80816;user=akashv2kart;password=SVXqEe5pDdamMb9;db=V2RETAIL;schema=GOLD;warehouse=V2_WH;role=ACCOUNTADMIN;";

        private static readonly Regex SafeId = new Regex(@"^[A-Za-z0-9_]{1,200}$");

        public static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !SafeId.IsMatch(name))
                throw new ArgumentException("Invalid identifier: " + name);
            return name.ToUpper();
        }

        private void AddParameters(IDbCommand cmd, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;
            foreach (var kv in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = kv.Key;
                p.DbType = DbType.String; // Always String — prevents AnsiString default on nulls

                if (kv.Value == null || kv.Value == DBNull.Value)
                {
                    p.Value = DBNull.Value;
                }
                else if (kv.Value is DateTime dt)
                {
                    p.Value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    p.Value = kv.Value.ToString();
                }
                cmd.Parameters.Add(p);
            }
        }

        public List<Dictionary<string, object>> QueryAsList(string sql, Dictionary<string, object> parameters = null)
        {
            var result = new List<Dictionary<string, object>>();
            using (var conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnStr;
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    AddParameters(cmd, parameters);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < rdr.FieldCount; i++)
                                row[rdr.GetName(i)] = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
                            result.Add(row);
                        }
                    }
                }
            }
            return result;
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null)
        {
            using (var conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnStr;
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    AddParameters(cmd, parameters);
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public object ExecuteScalar(string sql, Dictionary<string, object> parameters = null)
        {
            using (var conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnStr;
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    AddParameters(cmd, parameters);
                    var result = cmd.ExecuteScalar();
                    return result == DBNull.Value ? null : result;
                }
            }
        }

        /// <summary>
        /// Ensures the target table exists in Snowflake. Creates it with all VARCHAR columns
        /// if it does not exist yet. Safe to call on every insert — CREATE TABLE IF NOT EXISTS
        /// is a no-op when the table already exists.
        /// </summary>
        private void EnsureTableExists(string safeSchema, string safeTable, List<string> cols)
        {
            var colDefs = string.Join(", ", cols.ConvertAll(c => SanitizeIdentifier(c) + " VARCHAR"));
            ExecuteNonQuery(
                "CREATE TABLE IF NOT EXISTS " + safeSchema + "." + safeTable + " (" + colDefs + ")");
        }

        /// <summary>
        /// Bulk insert rows into a Snowflake target-schema table (default GOLD).
        /// Auto-creates the table if it does not exist (all VARCHAR columns).
        /// If dateColumn matches an actual output column and fromDate/toDate provided:
        ///   deletes existing rows for that date range first (upsert pattern).
        /// Inserts in chunks of 500 to stay within Snowflake limits.
        /// </summary>
        public int BulkInsert(string tableName, List<Dictionary<string, object>> rows,
            string dateColumn = null, DateTime? fromDate = null, DateTime? toDate = null,
            string targetSchema = "GOLD")
        {
            if (rows == null || rows.Count == 0) return 0;
            var safeTable  = SanitizeIdentifier(tableName);
            var safeSchema = SanitizeIdentifier(targetSchema ?? "GOLD");
            var cols = new List<string>(rows[0].Keys);

            // Auto-create table from first row's column names (all VARCHAR).
            EnsureTableExists(safeSchema, safeTable, cols);

            // Only delete the date range when dateColumn is an actual Snowflake output column.
            // Guards against RFC input parameter names (e.g. IM_DATE_FROM) being passed in,
            // which caused SQL compilation errors when the column did not exist in the table.
            bool dateColValid = dateColumn != null
                && fromDate != null
                && toDate != null
                && cols.Exists(c => string.Equals(c, dateColumn, StringComparison.OrdinalIgnoreCase));

            if (dateColValid)
            {
                string safeCol = SanitizeIdentifier(dateColumn);
                ExecuteNonQuery(
                    "DELETE FROM " + safeSchema + "." + safeTable +
                    " WHERE " + safeCol + " >= '" + fromDate.Value.ToString("yyyy-MM-dd") + "'" +
                    " AND "   + safeCol + " <= '" + toDate.Value.ToString("yyyy-MM-dd")   + "'");
            }

            int total = 0;
            using (var conn = new SnowflakeDbConnection())
            {
                conn.ConnectionString = ConnStr;
                conn.Open();
                const int CHUNK = 500;
                for (int i = 0; i < rows.Count; i += CHUNK)
                {
                    var chunk   = rows.GetRange(i, Math.Min(CHUNK, rows.Count - i));
                    var colList = string.Join(", ", cols.ConvertAll(c => SanitizeIdentifier(c)));
                    var valRows = new List<string>();
                    var paramMap = new Dictionary<string, object>();
                    int pIdx = 0;

                    foreach (var row in chunk)
                    {
                        var pNames = new List<string>();
                        foreach (var col in cols)
                        {
                            string pName = "p" + pIdx++;
                            pNames.Add(":" + pName);
                            paramMap[pName] = row.ContainsKey(col) ? (row[col] ?? DBNull.Value) : DBNull.Value;
                        }
                        valRows.Add("(" + string.Join(", ", pNames) + ")");
                    }

                    string insertSql = "INSERT INTO " + safeSchema + "." + safeTable +
                                       " (" + colList + ") VALUES " + string.Join(", ", valRows);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = insertSql;
                        AddParameters(cmd, paramMap);
                        total += cmd.ExecuteNonQuery();
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Atomic full-refresh load via PUT + INSERT OVERWRITE.
        /// Far faster than BulkInsert for large/wide rows because COPY-from-stage uses
        /// Snowflake's bulk path, not row-by-row VALUES.
        ///
        /// Behaviour:
        ///   1. CREATE TABLE IF NOT EXISTS &lt;schema&gt;.&lt;table&gt; with VARCHAR(&lt;sap_len&gt;)
        ///      for each SAP column + 4 audit columns (_LOADED_AT, _BATCH_ID,
        ///      _SOURCE_SYSTEM, _BUSINESS_DATE).
        ///   2. Write rows to a UTF-8 CSV in %TEMP%.
        ///   3. PUT 'file://...' @&lt;schema&gt;.%&lt;table&gt; AUTO_COMPRESS=TRUE OVERWRITE=TRUE.
        ///   4. INSERT OVERWRITE INTO &lt;schema&gt;.&lt;table&gt; (cols, audit cols) SELECT $1..$N,
        ///      CURRENT_TIMESTAMP(), :batchId, :sourceSystem, NULL FROM @stage/&lt;file&gt;.gz.
        ///   5. REMOVE staged file (best-effort).
        ///
        /// Returns rows actually landed (COUNT WHERE _BATCH_ID matches).
        /// </summary>
        public long BulkLoadViaStage(
            string targetSchema,
            string targetTable,
            List<SapTableDumpService.FieldMeta> sapCols,
            List<Dictionary<string, object>> rows,
            string batchId,
            string sourceSystem)
        {
            if (sapCols == null || sapCols.Count == 0)
                throw new ArgumentException("sapCols required", "sapCols");
            if (string.IsNullOrEmpty(batchId)) throw new ArgumentException("batchId required", "batchId");

            string safeSchema = SanitizeIdentifier(targetSchema ?? "RAW_SAP_MASTER");
            string safeTable  = SanitizeIdentifier(targetTable);
            string fq         = safeSchema + "." + safeTable;
            string stageRef   = "@" + safeSchema + ".%" + safeTable;

            // 1. CREATE TABLE IF NOT EXISTS with proper VARCHAR(length) + audit cols
            EnsureLandingTableExists(safeSchema, safeTable, sapCols);

            // 2. Write CSV
            string csvPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                safeTable + "_" + batchId.Substring(0, Math.Min(8, batchId.Length)) + ".csv");

            using (var sw = new System.IO.StreamWriter(csvPath, false, new System.Text.UTF8Encoding(false)))
            {
                foreach (var row in (rows ?? new List<Dictionary<string, object>>()))
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < sapCols.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        string v = "";
                        if (row != null && row.ContainsKey(sapCols[i].Name) && row[sapCols[i].Name] != null)
                            v = row[sapCols[i].Name].ToString();
                        sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
                    }
                    sw.WriteLine(sb.ToString());
                }
            }

            try
            {
                // 3. PUT to table stage
                string putPath = csvPath.Replace("\\", "/");
                ExecuteNonQuery("PUT 'file://" + putPath + "' " + stageRef +
                                " AUTO_COMPRESS=TRUE OVERWRITE=TRUE");

                // 4. INSERT OVERWRITE INTO ... (atomic replace)
                string sapColList = string.Join(", ", sapCols.ConvertAll(c => SanitizeIdentifier(c.Name)));
                string sapPositions = string.Join(", ",
                    Enumerable.Range(1, sapCols.Count).Select(i => "$" + i));
                string csvFileName = System.IO.Path.GetFileName(csvPath);

                // batchId is a server-issued Guid — safe to inline; sourceSystem is from app config.
                string sql =
                    "INSERT OVERWRITE INTO " + fq + " (" + sapColList +
                    ", _LOADED_AT, _BATCH_ID, _SOURCE_SYSTEM, _BUSINESS_DATE) " +
                    "SELECT " + sapPositions +
                    ", CURRENT_TIMESTAMP(), '" + batchId.Replace("'", "''") + "'" +
                    ", '" + (sourceSystem ?? "").Replace("'", "''") + "'" +
                    ", NULL " +
                    "FROM " + stageRef + "/" + csvFileName + ".gz " +
                    "(FILE_FORMAT => (TYPE='CSV' FIELD_OPTIONALLY_ENCLOSED_BY='\"' " +
                    "NULL_IF=('') EMPTY_FIELD_AS_NULL=TRUE))";
                ExecuteNonQuery(sql);

                // 5. Best-effort cleanup of the staged file
                try { ExecuteNonQuery("REMOVE " + stageRef + "/" + csvFileName + ".gz"); }
                catch { /* non-fatal */ }

                var landed = ExecuteScalar(
                    "SELECT COUNT(*) FROM " + fq + " WHERE _BATCH_ID = :bid",
                    new Dictionary<string, object> { { "bid", batchId } });
                return landed == null ? 0 : Convert.ToInt64(landed);
            }
            finally
            {
                try { if (System.IO.File.Exists(csvPath)) System.IO.File.Delete(csvPath); }
                catch { /* best effort */ }
            }
        }

        /// <summary>CREATE TABLE IF NOT EXISTS with SAP-correct VARCHAR(length) + 4 audit cols.</summary>
        private void EnsureLandingTableExists(string safeSchema, string safeTable,
            List<SapTableDumpService.FieldMeta> sapCols)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("CREATE TABLE IF NOT EXISTS ").Append(safeSchema).Append('.').Append(safeTable).Append(" (");
            foreach (var c in sapCols)
                sb.Append(SanitizeIdentifier(c.Name))
                  .Append(" VARCHAR(").Append(Math.Max(c.Length, 1)).Append("), ");
            sb.Append("_LOADED_AT TIMESTAMP_LTZ, ");
            sb.Append("_BATCH_ID VARCHAR, ");
            sb.Append("_SOURCE_SYSTEM VARCHAR, ");
            sb.Append("_BUSINESS_DATE DATE)");
            ExecuteNonQuery(sb.ToString());
        }

        /// <summary>Log RFC API call to GOLD.RFC_API_ACCESS_LOG (non-blocking).</summary>
        public void LogAccess(string requestId, string rfcCode, string endpoint,
            int status, long elapsedMs, int recordCount, string error = null)
        {
            try
            {
                ExecuteNonQuery(
                    @"INSERT INTO GOLD.RFC_API_ACCESS_LOG
                      (REQUEST_ID, RFC_CODE, HTTP_METHOD, ENDPOINT, RESPONSE_STATUS,
                       RESPONSE_TIME_MS, RECORDS_RETURNED, ERROR_MESSAGE)
                      VALUES (:rid, :rfc, 'POST', :ep, :st, :ms, :rc, :err)",
                    new Dictionary<string, object> {
                        { "rid", requestId ?? "" }, { "rfc", rfcCode ?? "" },
                        { "ep",  endpoint  ?? "" }, { "st",  status.ToString() },
                        { "ms",  elapsedMs.ToString() }, { "rc", recordCount.ToString() },
                        { "err", error ?? "" }
                    });
            }
            catch { /* non-blocking — don't fail the main request */ }
        }
    }
}
