using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.RfcSync;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// A — RFC Upload API
    /// Endpoint to upload an RFC document file.
    /// Stores metadata to [RFC_Upload_Metadata] table in MSSQL.
    /// Validates file type (xlsx, xls, csv, txt) and max size (50 MB).
    /// Route: POST /api/RfcUpload/Upload
    /// </summary>
    [RoutePrefix("api/RfcUpload")]
    public class RfcUploadController : BaseController
    {
        // ── Config ────────────────────────────────────────────────────────────────
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

        private static readonly string[] AllowedExtensions = { ".xlsx", ".xls", ".csv", ".txt", ".json" };
        private const long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB

        // ── Upload ───────────────────────────────────────────────────────────────
        [HttpPost]
        [Route("Upload")]
        public async Task<HttpResponseMessage> Upload()
        {
            try
            {
                if (!Request.Content.IsMimeMultipartContent())
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new RfcUploadResponse { Success = false, Message = "Request must be multipart/form-data." });

                var provider = new MultipartMemoryStreamProvider();
                await Request.Content.ReadAsMultipartAsync(provider);

                var filePart = provider.Contents.FirstOrDefault(c =>
                    c.Headers.ContentDisposition?.FileName != null);

                if (filePart == null)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new RfcUploadResponse { Success = false, Message = "No file found in request." });

                string originalName = filePart.Headers.ContentDisposition.FileName?.Trim('"') ?? "rfc_upload.xlsx";
                string ext = Path.GetExtension(originalName).ToLower();

                // Validate extension
                if (!AllowedExtensions.Contains(ext))
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new RfcUploadResponse { Success = false, Message = $"File type '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}" });

                byte[] fileBytes = await filePart.ReadAsByteArrayAsync();

                // Validate size
                if (fileBytes.Length > MaxFileSizeBytes)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new RfcUploadResponse { Success = false, Message = $"File size {fileBytes.Length:N0} bytes exceeds 50 MB limit." });

                // Store file in uploads folder under App_Data
                string uploadDir = HttpContext.Current != null
                    ? Path.Combine(HttpContext.Current.Server.MapPath("~/App_Data/RfcUploads"))
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "RfcUploads");

                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                string safeFileName = $"rfc_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                string filePath = Path.Combine(uploadDir, safeFileName);
                await File.WriteAllBytesAsync(filePath, fileBytes);

                // Get uploader info from header or default
                string uploadedBy = "system";
                if (Request.Headers.Contains("X-Uploaded-By"))
                    uploadedBy = Request.Headers.GetValues("X-Uploaded-By").FirstOrDefault() ?? "system";

                // Persist metadata to DB
                EnsureMetadataTable();
                int newId = InsertUploadMetadata(new RfcUploadMetadata
                {
                    FileName         = safeFileName,
                    OriginalFileName = originalName,
                    FilePath         = filePath,
                    FileSizeBytes    = fileBytes.Length,
                    ContentType      = filePart.Headers.ContentType?.MediaType ?? "application/octet-stream",
                    UploadedBy       = uploadedBy,
                    UploadedAt       = DateTime.Now,
                    Status           = "PENDING",
                    Remarks          = "Uploaded successfully. Awaiting processing."
                });

                return Request.CreateResponse(HttpStatusCode.OK, new RfcUploadResponse
                {
                    Success       = true,
                    Message       = "File uploaded successfully.",
                    UploadId      = newId,
                    FileName      = originalName,
                    FileSizeBytes = fileBytes.Length,
                    UploadedAt    = DateTime.Now,
                    Status        = "PENDING"
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new RfcUploadResponse { Success = false, Message = "Upload failed: " + ex.Message });
            }
        }

        // ── Get Upload Status ─────────────────────────────────────────────────────
        [HttpGet]
        [Route("Status/{uploadId:int}")]
        public HttpResponseMessage GetStatus(int uploadId)
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT Id, OriginalFileName, FileSizeBytes, UploadedBy, UploadedAt, Status, Remarks FROM RFC_Upload_Metadata WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = uploadId });
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return Request.CreateResponse(HttpStatusCode.NotFound,
                                    new { Success = false, Message = $"Upload ID {uploadId} not found." });

                            return Request.CreateResponse(HttpStatusCode.OK, new
                            {
                                Success          = true,
                                UploadId         = reader.GetInt32(0),
                                FileName         = reader.GetString(1),
                                FileSizeBytes    = reader.GetInt64(2),
                                UploadedBy       = reader.GetString(3),
                                UploadedAt       = reader.GetDateTime(4),
                                Status           = reader.GetString(5),
                                Remarks          = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Message = "Error fetching status: " + ex.Message });
            }
        }

        // ── List Uploads ──────────────────────────────────────────────────────────
        [HttpGet]
        [Route("List")]
        public HttpResponseMessage ListUploads()
        {
            try
            {
                var list = new System.Collections.Generic.List<object>();
                using (var conn = new SqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT Id, OriginalFileName, FileSizeBytes, UploadedBy, UploadedAt, Status, Remarks FROM RFC_Upload_Metadata ORDER BY UploadedAt DESC", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new
                            {
                                UploadId         = reader.GetInt32(0),
                                FileName         = reader.GetString(1),
                                FileSizeBytes    = reader.GetInt64(2),
                                UploadedBy       = reader.GetString(3),
                                UploadedAt       = reader.GetDateTime(4),
                                Status           = reader.GetString(5),
                                Remarks          = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Count = list.Count, Data = list });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Message = "Error: " + ex.Message });
            }
        }

        // ── Private Helpers ───────────────────────────────────────────────────────
        private void EnsureMetadataTable()
        {
            const string ddl = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'RFC_Upload_Metadata')
BEGIN
    CREATE TABLE [dbo].[RFC_Upload_Metadata] (
        [Id]               INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [FileName]         NVARCHAR(500)  NOT NULL,
        [OriginalFileName] NVARCHAR(500)  NOT NULL,
        [FilePath]         NVARCHAR(1000) NOT NULL,
        [FileSizeBytes]    BIGINT         NOT NULL DEFAULT 0,
        [ContentType]      NVARCHAR(200)  NULL,
        [UploadedBy]       NVARCHAR(200)  NOT NULL DEFAULT 'system',
        [UploadedAt]       DATETIME       NOT NULL DEFAULT GETDATE(),
        [Status]           NVARCHAR(50)   NOT NULL DEFAULT 'PENDING',
        [Remarks]          NVARCHAR(MAX)  NULL
    );
END";
            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand(ddl, conn))
                    cmd.ExecuteNonQuery();
            }
        }

        private int InsertUploadMetadata(RfcUploadMetadata meta)
        {
            const string sql = @"
INSERT INTO [RFC_Upload_Metadata]
    (FileName, OriginalFileName, FilePath, FileSizeBytes, ContentType, UploadedBy, UploadedAt, Status, Remarks)
VALUES
    (@FileName, @OriginalFileName, @FilePath, @FileSizeBytes, @ContentType, @UploadedBy, @UploadedAt, @Status, @Remarks);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var conn = new SqlConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@FileName",         SqlDbType.NVarChar, 500)  { Value = meta.FileName });
                    cmd.Parameters.Add(new SqlParameter("@OriginalFileName", SqlDbType.NVarChar, 500)  { Value = meta.OriginalFileName });
                    cmd.Parameters.Add(new SqlParameter("@FilePath",         SqlDbType.NVarChar, 1000) { Value = meta.FilePath });
                    cmd.Parameters.Add(new SqlParameter("@FileSizeBytes",    SqlDbType.BigInt)         { Value = meta.FileSizeBytes });
                    cmd.Parameters.Add(new SqlParameter("@ContentType",      SqlDbType.NVarChar, 200)  { Value = (object)meta.ContentType ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@UploadedBy",       SqlDbType.NVarChar, 200)  { Value = meta.UploadedBy });
                    cmd.Parameters.Add(new SqlParameter("@UploadedAt",       SqlDbType.DateTime)       { Value = meta.UploadedAt });
                    cmd.Parameters.Add(new SqlParameter("@Status",           SqlDbType.NVarChar, 50)   { Value = meta.Status });
                    cmd.Parameters.Add(new SqlParameter("@Remarks",          SqlDbType.NVarChar, -1)   { Value = (object)meta.Remarks ?? DBNull.Value });
                    return (int)cmd.ExecuteScalar();
                }
            }
        }
    }
}
