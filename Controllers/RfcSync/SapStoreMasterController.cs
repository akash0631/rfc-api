using SAP.Middleware.Connector;
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
using System.Web.Http.Description;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// SAP Store Master API — Pipeline 1 + Pipeline 2
    ///
    /// PIPELINE 1 (Direct RFC → App):
    ///   GET  /api/SapStoreMaster          — fetch live store master from SAP, return JSON
    ///   GET  /api/SapStoreMaster/{werks}  — single store by plant code
    ///
    /// PIPELINE 2 (RFC → Data Lake → Query API):
    ///   POST /api/SapStoreMaster/SyncToDataLake  — fetch from SAP + upsert to SAP_STORE_MASTER table
    ///   Then query via: GET /api/SapDataLake/Query/SAP_STORE_MASTER
    ///
    /// Data source: SAP T001W (Plant Master) via RFC_READ_TABLE.
    /// No CSV, no file upload — data flows directly from SAP RFC to data lake.
    /// </summary>
    [RoutePrefix("api/SapStoreMaster")]
    public class SapStoreMasterController : BaseController
    {
        // ── SAP field layout for T001W ────────────────────────────────────────
        private static readonly FieldDef[] FIELDS = {
            new FieldDef("WERKS", 0,   4,  "Plant/Store Code"),
            new FieldDef("NAME1", 4,   30, "Store Name"),
            new FieldDef("NAME2", 34,  30, "Name 2"),
            new FieldDef("STRAS", 64,  30, "Street Address"),
            new FieldDef("ORT01", 94,  25, "City"),
            new FieldDef("PSTLZ", 119, 10, "Postal Code"),
            new FieldDef("LAND1", 129, 3,  "Country"),
            new FieldDef("REGIO", 132, 3,  "State/Region Code"),
        };

        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

        private const string TABLE_NAME = "SAP_STORE_MASTER";
        private const string KEY_COL    = "WERKS";

        // ── PIPELINE 1: Direct SAP fetch ─────────────────────────────────────

        /// <summary>
        /// [PIPELINE 1] Fetch all store master records live from SAP T001W.
        /// Returns structured JSON — no SAP WA parsing needed by the caller.
        /// Any application (HHT, dashboard, analytics) can call this directly.
        /// Optional filter: ?country=IN to get only Indian stores.
        /// </summary>
        [HttpGet]
        [Route("")]
        [ResponseType(typeof(SapStoreMasterResponse))]
        public HttpResponseMessage GetAll(string country = null, int maxRows = 1000)
        {
            try
            {
                var records = FetchFromSap(maxRows);

                if (!string.IsNullOrWhiteSpace(country))
                    records = records.Where(r =>
                        r.ContainsKey("LAND1") &&
                        string.Equals(r["LAND1"], country.Trim(), StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Success     = true,
                    Source      = "SAP-LIVE",
                    SapTable    = "T001W",
                    Count       = records.Count,
                    FetchedAt   = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    Fields      = FIELDS.Select(f => new { f.Name, f.Description }),
                    Data        = records
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// [PIPELINE 1] Fetch a single store by plant code (WERKS).
        /// Returns full address details from SAP T001W.
        /// </summary>
        [HttpGet]
        [Route("{werks}")]
        [ResponseType(typeof(SapStoreMasterResponse))]
        public HttpResponseMessage GetOne(string werks)
        {
            if (string.IsNullOrWhiteSpace(werks) || werks.Length > 4)
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Success = false, Error = "WERKS must be 1-4 characters." });
            try
            {
                var options = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { ["TEXT"] = "WERKS = '" + werks.ToUpper().Trim() + "'" }
                };
                var records = FetchFromSap(1, options);
                if (records.Count == 0)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new { Success = false, Error = "Store '" + werks + "' not found in SAP T001W." });

                return Request.CreateResponse(HttpStatusCode.OK,
                    new { Success = true, Source = "SAP-LIVE", Data = records[0] });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── PIPELINE 2: RFC → Data Lake ───────────────────────────────────────

        /// <summary>
        /// [PIPELINE 2] Fetch store master from SAP T001W and upsert into the
        /// SAP_STORE_MASTER data lake table. Table is auto-created on first run.
        /// Schema evolves automatically as SAP fields change.
        ///
        /// After sync, query via: GET /api/SapDataLake/Query/SAP_STORE_MASTER
        ///
        /// This is the correct approach — data flows directly from SAP RFC
        /// through the RFC API into the data lake. No CSV, no file upload.
        /// </summary>
        [HttpPost]
        [Route("SyncToDataLake")]
        [ResponseType(typeof(SapStoreMasterSyncResult))]
        public HttpResponseMessage SyncToDataLake([FromBody] StoreMasterSyncRequest req)
        {
            int maxRows = req?.MaxRows > 0 ? req.MaxRows : 5000;
            string countryFilter = req?.CountryFilter;

            try
            {
                // Step 1: Fetch live from SAP
                var records = FetchFromSap(maxRows);

                if (!string.IsNullOrWhiteSpace(countryFilter))
                    records = records.Where(r =>
                        r.ContainsKey("LAND1") &&
                        string.Equals(r["LAND1"], countryFilter.Trim(), StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                if (records.Count == 0)
                    return Request.CreateResponse(HttpStatusCode.OK,
                        new { Success = false, Error = "SAP returned 0 records. Check RFC connectivity." });

                // Step 2: Upsert into data lake (HUCreation.SAP_STORE_MASTER)
                int upserted = SyncToSql(records);

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Success          = true,
                    Pipeline         = "SAP-RFC → SAP_STORE_MASTER → Query API",
                    SapSource        = "T001W via RFC_READ_TABLE",
                    DataLakeTable    = TABLE_NAME,
                    QueryApi         = "/api/SapDataLake/Query/" + TABLE_NAME,
                    FetchedFromSap   = records.Count,
                    UpsertedToLake   = upserted,
                    CountryFilter    = countryFilter ?? "ALL",
                    SyncedAt         = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Error = ex.Message });
            }
        }

        // ── SAP Fetch via RFC_READ_TABLE ──────────────────────────────────────
        private List<Dictionary<string, string>> FetchFromSap(
            int maxRows = 1000,
            List<Dictionary<string, string>> options = null)
        {
            RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
            RfcDestination dest        = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction rfc           = dest.Repository.CreateFunction("RFC_READ_TABLE");

            rfc.SetValue("QUERY_TABLE", "T001W");
            rfc.SetValue("DELIMITER",   "|");
            rfc.SetValue("ROWCOUNT",    maxRows.ToString());
            rfc.SetValue("NO_DATA",     "");

            // Set FIELDS table
            IRfcTable fieldsTable = rfc.GetTable("FIELDS");
            foreach (var f in FIELDS)
            {
                fieldsTable.Append();
                fieldsTable.SetValue("FIELDNAME", f.Name);
            }

            // Set OPTIONS (WHERE clause filter)
            if (options != null && options.Count > 0)
            {
                IRfcTable optTable = rfc.GetTable("OPTIONS");
                foreach (var opt in options)
                {
                    optTable.Append();
                    optTable.SetValue("TEXT", opt["TEXT"]);
                }
            }

            rfc.Invoke(dest);

            // Parse DATA table (pipe-delimited WA format)
            IRfcTable dataTable = rfc.GetTable("DATA");
            var records = new List<Dictionary<string, string>>();

            foreach (IRfcStructure row in dataTable)
            {
                string wa = row.GetString("WA");
                if (string.IsNullOrWhiteSpace(wa)) continue;

                string[] parts = wa.Split('|');
                var rec = new Dictionary<string, string>();
                for (int i = 0; i < FIELDS.Length && i < parts.Length; i++)
                    rec[FIELDS[i].Name] = parts[i].Trim();

                if (rec.TryGetValue("WERKS", out string werks) && !string.IsNullOrEmpty(werks))
                    records.Add(rec);
            }

            return records;
        }

        // ── SQL Upsert ────────────────────────────────────────────────────────
        private int SyncToSql(List<Dictionary<string, string>> records)
        {
            var cols = FIELDS.Select(f => f.Name).ToList();

            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();

                // Create table if not exists
                if (!TableExists(conn))
                    CreateTable(conn, cols);

                // MERGE upsert via temp table
                string tmpTable = "#tmp_store_master";
                string colList  = string.Join(", ", cols.Select(c => "[" + c + "]"));
                string insertVals = string.Join(", ", cols.Select(c => "S.[" + c + "]"));
                string updateSet  = string.Join(", ",
                    cols.Where(c => c != KEY_COL).Select(c => "T.[" + c + "]=S.[" + c + "]"));

                // Ensure all columns exist (schema evolution for existing tables)
                EnsureColumns(conn, cols);

                // Create temp
                string createTmp = "SELECT TOP 0 " + colList + " INTO " + tmpTable +
                                   " FROM [dbo].[" + TABLE_NAME + "]";
                using (var cmd = new SqlCommand(createTmp, conn))
                    cmd.ExecuteNonQuery();

                // Bulk insert into temp
                string insTmp = "INSERT INTO " + tmpTable + " (" + colList + ") VALUES (" +
                                string.Join(", ", cols.Select(c => "@" + c)) + ")";

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var rec in records)
                        {
                            using (var cmd = new SqlCommand(insTmp, conn, tx))
                            {
                                foreach (var col in cols)
                                {
                                    rec.TryGetValue(col, out string val);
                                    cmd.Parameters.Add(new SqlParameter("@" + col, SqlDbType.NVarChar, 500)
                                        { Value = (object)val ?? DBNull.Value });
                                }
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // MERGE
                        string merge =
                            "MERGE [dbo].[" + TABLE_NAME + "] WITH (HOLDLOCK) AS T " +
                            "USING " + tmpTable + " AS S ON T.[" + KEY_COL + "]=S.[" + KEY_COL + "] " +
                            "WHEN MATCHED THEN UPDATE SET " + updateSet + ", T.[_SYNCED_AT]=GETDATE() " +
                            "WHEN NOT MATCHED THEN INSERT (" + colList + ", [_SYNCED_AT]) " +
                            "VALUES (" + insertVals + ", GETDATE());";

                        int count;
                        using (var cmd = new SqlCommand(merge, conn, tx))
                            count = cmd.ExecuteNonQuery();

                        tx.Commit();
                        return count;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private void EnsureColumns(SqlConnection conn, List<string> cols)
        {
            // Get existing columns
            var existing = new List<string>();
            using (var cmd = new SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@T", conn))
            {
                cmd.Parameters.AddWithValue("@T", TABLE_NAME);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read()) existing.Add(rdr.GetString(0).ToUpper());
            }
            // Add any missing columns
            foreach (var col in cols.Where(c => !existing.Contains(c.ToUpper())))
            {
                string sql = "ALTER TABLE [dbo].[" + TABLE_NAME + "] ADD [" + col + "] NVARCHAR(500) NULL";
                using (var cmd = new SqlCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }
        }

        private bool TableExists(SqlConnection conn)
        {
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@T", conn))
            {
                cmd.Parameters.AddWithValue("@T", TABLE_NAME);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        private void CreateTable(SqlConnection conn, List<string> cols)
        {
            string colDefs = string.Join(", ",
                cols.Select(c => "[" + c + "] NVARCHAR(500) " + (c == KEY_COL ? "NOT NULL" : "NULL")));

            string sql = "CREATE TABLE [dbo].[" + TABLE_NAME + "] (" +
                         colDefs + ", [_SYNCED_AT] DATETIME DEFAULT GETDATE(), " +
                         "CONSTRAINT [PK_" + TABLE_NAME + "] PRIMARY KEY ([" + KEY_COL + "]))";

            using (var cmd = new SqlCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        // ── Inner types ───────────────────────────────────────────────────────
        private class FieldDef
        {
            public string Name, Description;
            public int Offset, Length;
            public FieldDef(string n, int o, int l, string d)
            { Name = n; Offset = o; Length = l; Description = d; }
        }
    }

    // ── Request / Response models ─────────────────────────────────────────────
    /// <summary>Request body for SyncToDataLake.</summary>
    public class StoreMasterSyncRequest
    {
        /// <summary>Max rows to fetch from SAP T001W (default 5000).</summary>
        public int MaxRows { get; set; }
        /// <summary>Optional ISO country code filter e.g. "IN" for India only.</summary>
        public string CountryFilter { get; set; }
    }

    /// <summary>Response wrapper for store master fetch.</summary>
    public class SapStoreMasterResponse
    {
        public bool Success { get; set; }
        public string Source { get; set; }
        public int Count { get; set; }
        public List<Dictionary<string, string>> Data { get; set; }
    }

    /// <summary>Result of SyncToDataLake operation.</summary>
    public class SapStoreMasterSyncResult
    {
        public bool Success { get; set; }
        public string Pipeline { get; set; }
        public string DataLakeTable { get; set; }
        public string QueryApi { get; set; }
        public int FetchedFromSap { get; set; }
        public int UpsertedToLake { get; set; }
    }
}
