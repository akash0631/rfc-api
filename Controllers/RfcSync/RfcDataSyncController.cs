using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.RfcSync;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// D — RFC Data Sync API  (E — Data Check API included)
    /// POST /api/RfcDataSync/Sync   — full idempotent upsert
    /// POST /api/RfcDataSync/Check  — check if records exist for date/range
    /// </summary>
    [RoutePrefix("api/RfcDataSync")]
    public class RfcDataSyncController : BaseController
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

        private static readonly Regex SafeTableName = new Regex("^[A-Za-z0-9_]{1,128}$");

        // ── D: Sync Data ────────────────────────────────────────────────────
        [HttpPost]
        [Route("Sync")]
        public HttpResponseMessage Sync([FromBody] RfcSyncRequest request)
        {
            // 1. Validate request
            var validation = ValidateSyncRequest(request);
            if (validation != null)
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new RfcSyncResponse { Success = false, Message = validation });

            string table = request.TableName;
            bool tableCreated = false;
            bool tableAlreadyExisted = false;
            int existingRows = 0;
            int deletedRows = 0;
            int insertedRows = 0;

            try
            {
                // 2. Confirm/create destination table
                tableAlreadyExisted = RfcTableController.TableExists(table);
                if (!tableAlreadyExisted)
                {
                    RfcTableController.CreateRfcDataTable(table);
                    tableCreated = true;
                }

                // 3. Pull data from the parsed RFC file
                string filePath = GetFilePath(request.UploadId);
                if (filePath == null || !File.Exists(filePath))
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new RfcSyncResponse { Success = false, Message = "Upload file not found. Upload and process first." });

                var allRows = ParseCsvFile(filePath);
                var filteredRows = FilterByDate(allRows, request);

                // 4. Check existing records in MSSQL
                existingRows = CountExistingRows(table, request);

                // 5. Delete if exist
                if (existingRows > 0)
                    deletedRows = DeleteExistingRows(table, request);

                // 6. Insert fresh data
                insertedRows = BulkInsert(table, filteredRows);

                string dateFilter = BuildDateLabel(request);

                // 7. Return summary
                return Request.CreateResponse(HttpStatusCode.OK, new RfcSyncResponse
                {
                    Success              = true,
                    Message              = "Sync completed successfully.",
                    TableName            = table,
                    TableCreated         = tableCreated,
                    TableAlreadyExisted  = tableAlreadyExisted,
                    ExistingRowsFound    = existingRows,
                    DeletedRowsCount     = deletedRows,
                    InsertedRowsCount    = insertedRows,
                    RequestedDate        = dateFilter,
                    Status               = "SUCCESS",
                    SyncedAt             = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new RfcSyncResponse { Success = false, Message = "Sync failed: " + ex.Message, Status = "FAILED" });
            }
        }

        // ── E: Check Existing Data ───────────────────────────────────────────
        [HttpPost]
        [Route("Check")]
        public HttpResponseMessage Check([FromBody] RfcDataCheckRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TableName))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new RfcDataCheckResponse { Success = false, Message = "TableName is required." });

            if (!SafeTableName.IsMatch(request.TableName))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new RfcDataCheckResponse { Success = false, Message = "Invalid table name." });

            try
            {
                if (!RfcTableController.TableExists(request.TableName))
                    return Request.CreateResponse(HttpStatusCode.OK, new RfcDataCheckResponse
                    {
                        Success = true, TableName = request.TableName,
                        RecordsExist = false, RecordCount = 0,
                        Message = "Table does not exist yet."
                    });

                int count = CountExistingRows(request.TableName, AsSyncRequest(request));
                string label = BuildDateLabel(AsSyncRequest(request));

                return Request.CreateResponse(HttpStatusCode.OK, new RfcDataCheckResponse
                {
                    Success      = true,
                    TableName    = request.TableName,
                    RecordsExist = count > 0,
                    RecordCount  = count,
                    DateFilter   = label,
                    Message      = count > 0 ? count + " records found for " + label : "No records found for " + label
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new RfcDataCheckResponse { Success = false, Message = "Error: " + ex.Message });
            }
        }

        // ── Private Helpers ─────────────────────────────────────────────────
        private string ValidateSyncRequest(RfcSyncRequest r)
        {
            if (r == null) return "Request body is required.";
            if (string.IsNullOrWhiteSpace(r.TableName)) return "TableName is required.";
            if (!SafeTableName.IsMatch(r.TableName)) return "TableName contains invalid characters.";
            if (r.UploadId <= 0) return "UploadId must be > 0.";
            if (r.SyncType == SyncType.PARTICULAR_DATE && r.Date == null) return "Date is required for PARTICULAR_DATE sync.";
            if (r.SyncType == SyncType.DATE_RANGE && (r.FromDate == null || r.ToDate == null)) return "FromDate and ToDate are required for DATE_RANGE sync.";
            if (r.SyncType == SyncType.DATE_RANGE && r.FromDate > r.ToDate) return "FromDate must be <= ToDate.";
            return null;
        }

        private List<RfcDataRow> FilterByDate(List<RfcDataRow> rows, RfcSyncRequest r)
        {
            if (r.SyncType == SyncType.PARTICULAR_DATE && r.Date != null)
                return rows.FindAll(x => x.RecordDate.Date == r.Date.Value.Date);
            if (r.SyncType == SyncType.DATE_RANGE && r.FromDate != null && r.ToDate != null)
                return rows.FindAll(x => x.RecordDate.Date >= r.FromDate.Value.Date && x.RecordDate.Date <= r.ToDate.Value.Date);
            return rows;
        }

        private int CountExistingRows(string table, RfcSyncRequest r)
        {
            string whereClause = BuildWhereClause(r.SyncType, r.Date, r.FromDate, r.ToDate);
            string sql = "SELECT COUNT(1) FROM [dbo].[" + table + "] WHERE " + whereClause;
            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    AddDateParams(cmd, r.SyncType, r.Date, r.FromDate, r.ToDate);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private int DeleteExistingRows(string table, RfcSyncRequest r)
        {
            string whereClause = BuildWhereClause(r.SyncType, r.Date, r.FromDate, r.ToDate);
            string sql = "DELETE FROM [dbo].[" + table + "] WHERE " + whereClause;
            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    AddDateParams(cmd, r.SyncType, r.Date, r.FromDate, r.ToDate);
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        private int BulkInsert(string table, List<RfcDataRow> rows)
        {
            if (rows == null || rows.Count == 0) return 0;
            string insertSql =
                "INSERT INTO [dbo].[" + table + "] " +
                "(RecordDate,RfcName,FunctionModule,Description,Status,Category,Parameters,CreatedBy,Remarks,SyncedAt) " +
                "VALUES (@RecordDate,@RfcName,@FunctionModule,@Description,@Status,@Category,@Parameters,@CreatedBy,@Remarks,@SyncedAt)";

            int count = 0;
            DateTime syncedAt = DateTime.Now;

            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var row in rows)
                        {
                            using (var cmd = new SqlCommand(insertSql, conn, tx))
                            {
                                cmd.Parameters.Add(new SqlParameter("@RecordDate",     SqlDbType.Date)         { Value = row.RecordDate.Date });
                                cmd.Parameters.Add(new SqlParameter("@RfcName",        SqlDbType.NVarChar, 200){ Value = Nz(row.RfcName) });
                                cmd.Parameters.Add(new SqlParameter("@FunctionModule", SqlDbType.NVarChar, 200){ Value = Nz(row.FunctionModule) });
                                cmd.Parameters.Add(new SqlParameter("@Description",   SqlDbType.NVarChar, 1000){ Value = Nz(row.Description) });
                                cmd.Parameters.Add(new SqlParameter("@Status",        SqlDbType.NVarChar, 100){ Value = Nz(row.Status) });
                                cmd.Parameters.Add(new SqlParameter("@Category",      SqlDbType.NVarChar, 200){ Value = Nz(row.Category) });
                                cmd.Parameters.Add(new SqlParameter("@Parameters",    SqlDbType.NVarChar, -1) { Value = Nz(row.Parameters) });
                                cmd.Parameters.Add(new SqlParameter("@CreatedBy",     SqlDbType.NVarChar, 200){ Value = Nz(row.CreatedBy) });
                                cmd.Parameters.Add(new SqlParameter("@Remarks",       SqlDbType.NVarChar, -1) { Value = Nz(row.Remarks) });
                                cmd.Parameters.Add(new SqlParameter("@SyncedAt",      SqlDbType.DateTime)     { Value = syncedAt });
                                count += cmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
            return count;
        }

        // ── SQL Helpers (all parameterised) ─────────────────────────────────
        private string BuildWhereClause(SyncType syncType, DateTime? date, DateTime? from, DateTime? to)
        {
            if (syncType == SyncType.PARTICULAR_DATE)
                return "CAST(RecordDate AS DATE) = @Date";
            return "CAST(RecordDate AS DATE) >= @FromDate AND CAST(RecordDate AS DATE) <= @ToDate";
        }

        private void AddDateParams(SqlCommand cmd, SyncType syncType, DateTime? date, DateTime? from, DateTime? to)
        {
            if (syncType == SyncType.PARTICULAR_DATE)
                cmd.Parameters.Add(new SqlParameter("@Date", SqlDbType.Date) { Value = date.Value.Date });
            else
            {
                cmd.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.Date) { Value = from.Value.Date });
                cmd.Parameters.Add(new SqlParameter("@ToDate",   SqlDbType.Date) { Value = to.Value.Date });
            }
        }

        private string BuildDateLabel(RfcSyncRequest r)
        {
            if (r.SyncType == SyncType.PARTICULAR_DATE && r.Date != null)
                return r.Date.Value.ToString("yyyy-MM-dd");
            if (r.SyncType == SyncType.DATE_RANGE && r.FromDate != null && r.ToDate != null)
                return r.FromDate.Value.ToString("yyyy-MM-dd") + " to " + r.ToDate.Value.ToString("yyyy-MM-dd");
            return "all dates";
        }

        private object Nz(string val) => string.IsNullOrEmpty(val) ? (object)DBNull.Value : val;

        private RfcSyncRequest AsSyncRequest(RfcDataCheckRequest r)
        {
            return new RfcSyncRequest
            {
                TableName = r.TableName,
                SyncType  = r.SyncType,
                Date      = r.Date,
                FromDate  = r.FromDate,
                ToDate    = r.ToDate,
                UploadId  = 0
            };
        }

        private string GetFilePath(int uploadId)
        {
            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT FilePath FROM RFC_Upload_Metadata WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", uploadId);
                    var r = cmd.ExecuteScalar();
                    return r != null ? r.ToString() : null;
                }
            }
        }

        private List<RfcDataRow> ParseCsvFile(string filePath)
        {
            var rows = new List<RfcDataRow>();
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return rows;
            char delim = lines[0].Contains(",") ? ',' : '\t';
            string[] headers = lines[0].Split(delim);
            var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++) idx[headers[i].Trim()] = i;
            for (int n = 1; n < lines.Length; n++)
            {
                if (string.IsNullOrWhiteSpace(lines[n])) continue;
                string[] cols = lines[n].Split(delim);
                rows.Add(new RfcDataRow
                {
                    RecordDate     = GetDate(cols, idx, "RecordDate"),
                    RfcName        = GetStr(cols, idx, "RfcName"),
                    FunctionModule = GetStr(cols, idx, "FunctionModule"),
                    Description    = GetStr(cols, idx, "Description"),
                    Status         = GetStr(cols, idx, "Status"),
                    Category       = GetStr(cols, idx, "Category"),
                    Parameters     = GetStr(cols, idx, "Parameters"),
                    CreatedBy      = GetStr(cols, idx, "CreatedBy"),
                    Remarks        = GetStr(cols, idx, "Remarks")
                });
            }
            return rows;
        }

        private string GetStr(string[] cols, Dictionary<string, int> idx, string key)
        { if (idx.TryGetValue(key, out int i) && i < cols.Length) return cols[i].Trim(); return string.Empty; }

        private DateTime GetDate(string[] cols, Dictionary<string, int> idx, string key)
        { string v = GetStr(cols, idx, key); return DateTime.TryParse(v, out var dt) ? dt : DateTime.Today; }
    }
}
