using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.RfcSync;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// B — RFC Processing API
    /// Reads uploaded RFC document and parses its content.
    /// Route: POST /api/RfcProcess/Parse
    ///        GET  /api/RfcProcess/ParsedData/{uploadId}
    /// </summary>
    [RoutePrefix("api/RfcProcess")]
    public class RfcProcessController : BaseController
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

        // ── Parse RFC Document ───────────────────────────────────────────
        [HttpPost]
        [Route("Parse")]
        public HttpResponseMessage Parse([FromBody] RfcProcessRequest request)
        {
            if (request == null || request.UploadId <= 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new RfcProcessResponse { Success = false, Message = "UploadId is required." });

            try
            {
                string filePath = GetFilePath(request.UploadId);
                if (filePath == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new RfcProcessResponse { Success = false, Message = "Upload not found." });

                if (!File.Exists(filePath))
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new RfcProcessResponse { Success = false, Message = "File not on disk." });

                var rows = ParseCsvFile(filePath);
                UpdateUploadStatus(request.UploadId, "PROCESSED", "Parsed " + rows.Count + " rows.");

                return Request.CreateResponse(HttpStatusCode.OK, new RfcProcessResponse
                {
                    Success   = true,
                    Message   = "Parsed " + rows.Count + " rows successfully.",
                    UploadId  = request.UploadId,
                    TotalRows = rows.Count,
                    Data      = rows
                });
            }
            catch (Exception ex)
            {
                UpdateUploadStatus(request.UploadId, "FAILED", ex.Message);
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new RfcProcessResponse { Success = false, Message = "Parse failed: " + ex.Message });
            }
        }

        // ── Get Parsed Data with optional date filter ─────────────────────
        [HttpGet]
        [Route("ParsedData/{uploadId:int}")]
        public HttpResponseMessage GetParsedData(int uploadId,
            [FromUri] string date = null, [FromUri] string fromDate = null, [FromUri] string toDate = null)
        {
            try
            {
                string filePath = GetFilePath(uploadId);
                if (filePath == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound,
                        new { Success = false, Message = "Upload not found." });

                var rows = ParseCsvFile(filePath);

                if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var dt))
                    rows = rows.FindAll(r => r.RecordDate.Date == dt.Date);
                else if (!string.IsNullOrWhiteSpace(fromDate) && !string.IsNullOrWhiteSpace(toDate)
                         && DateTime.TryParse(fromDate, out var from) && DateTime.TryParse(toDate, out var to))
                    rows = rows.FindAll(r => r.RecordDate.Date >= from.Date && r.RecordDate.Date <= to.Date);

                return Request.CreateResponse(HttpStatusCode.OK,
                    new { Success = true, UploadId = uploadId, TotalRows = rows.Count, Data = rows });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Message = "Error: " + ex.Message });
            }
        }

        // ── Private Helpers ────────────────────────────────────────────────
        /// <summary>
        /// Parses CSV / tab-delimited RFC document.
        /// Expected header columns: RecordDate,RfcName,FunctionModule,Description,Status,Category,Parameters,CreatedBy,Remarks
        /// Missing columns are tolerated gracefully.
        /// </summary>
        private List<RfcDataRow> ParseCsvFile(string filePath)
        {
            var rows = new List<RfcDataRow>();
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return rows;

            char delim = lines[0].Contains(",") ? ',' : '\t';
            string[] headers = lines[0].Split(delim);
            var colIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                colIdx[headers[i].Trim()] = i;

            for (int n = 1; n < lines.Length; n++)
            {
                if (string.IsNullOrWhiteSpace(lines[n])) continue;
                string[] cols = lines[n].Split(delim);
                rows.Add(new RfcDataRow
                {
                    RecordDate     = GetDate(cols, colIdx, "RecordDate"),
                    RfcName        = GetStr(cols, colIdx, "RfcName"),
                    FunctionModule = GetStr(cols, colIdx, "FunctionModule"),
                    Description    = GetStr(cols, colIdx, "Description"),
                    Status         = GetStr(cols, colIdx, "Status"),
                    Category       = GetStr(cols, colIdx, "Category"),
                    Parameters     = GetStr(cols, colIdx, "Parameters"),
                    CreatedBy      = GetStr(cols, colIdx, "CreatedBy"),
                    Remarks        = GetStr(cols, colIdx, "Remarks")
                });
            }
            return rows;
        }

        private string GetStr(string[] cols, Dictionary<string, int> idx, string key)
        {
            if (idx.TryGetValue(key, out int i) && i < cols.Length) return cols[i].Trim();
            return string.Empty;
        }

        private DateTime GetDate(string[] cols, Dictionary<string, int> idx, string key)
        {
            string val = GetStr(cols, idx, key);
            return DateTime.TryParse(val, out var dt) ? dt : DateTime.Today;
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

        private void UpdateUploadStatus(int uploadId, string status, string remarks)
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "UPDATE RFC_Upload_Metadata SET Status=@S, Remarks=@R WHERE Id=@Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@S", status);
                        cmd.Parameters.AddWithValue("@R", remarks ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", uploadId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { /* best-effort */ }
        }
    }
}
