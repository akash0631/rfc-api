using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text.RegularExpressions;
using Snowflake.Data.Client;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// Snowflake connectivity service for Snowflake.Data .NET driver 2.1.5.
    ///
    /// KEY COMPATIBILITY FIX:
    ///   Snowflake.Data 2.1.5 throws "No corresponding Snowflake type for type AnsiString"
    ///   when parameter DbType is inferred from a boxed .NET value.
    ///   Fix: always pass parameter values as string with DbType.String.
    ///   Snowflake handles implicit type coercion (int, date, boolean all work from string).
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

                if (kv.Value == null || kv.Value == DBNull.Value)
                {
                    p.Value = DBNull.Value;
                }
                else
                {
                    // Convert all values to string — Snowflake coerces implicitly.
                    // Avoids Snowflake.Data 2.x "No corresponding type for AnsiString" error.
                    if (kv.Value is DateTime dt)
                        p.Value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                    else
                        p.Value = kv.Value.ToString();

                    p.DbType = DbType.String;
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
        /// Bulk insert rows into a Snowflake GOLD table.
        /// If dateColumn + fromDate/toDate provided: deletes existing rows for that range first.
        /// Inserts in chunks of 500 to stay within Snowflake limits.
        /// </summary>
        public int BulkInsert(string tableName, List<Dictionary<string, object>> rows,
            string dateColumn = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (rows == null || rows.Count == 0) return 0;
            var safeTable = SanitizeIdentifier(tableName);
            var cols = new List<string>(rows[0].Keys);

            // Delete existing range before upsert
            if (dateColumn != null && fromDate != null && toDate != null)
            {
                string safeCol = SanitizeIdentifier(dateColumn);
                ExecuteNonQuery(
                    "DELETE FROM GOLD." + safeTable +
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
                    var chunk  = rows.GetRange(i, Math.Min(CHUNK, rows.Count - i));
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

                    string insertSql = "INSERT INTO GOLD." + safeTable + " (" + colList + ") VALUES " + string.Join(", ", valRows);
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
