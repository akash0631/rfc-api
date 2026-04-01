using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.RfcSync;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    [RoutePrefix("api/RfcUpload")]
    public class RfcUploadController : BaseController
    {
        private static readonly string[] AllowedExtensions = { ".csv", ".tsv", ".txt", ".xlsx", ".xls", ".pdf", ".docx", ".doc" };
        private const long MaxFileSizeBytes = 50 * 1024 * 1024;

        private string GetConnectionString()
        {
            var cs = ConfigurationManager.ConnectionStrings["RfcSync"]
                  ?? ConfigurationManager.ConnectionStrings["HuCreation"];
            if (cs == null) throw new InvalidOperationException("No RfcSync or HuCreation connection string found.");
            return cs.ConnectionString;
        }

        private string GetUploadRoot()
        {
            var path = HttpContext.Current != null
                ? HttpContext.Current.Server.MapPath("~/UploadedFiles/RfcDocuments/")
                : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UploadedFiles", "RfcDocuments");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private string BuildDownloadUrl(int uploadId)
        {
            return "/api/RfcUpload/Download/" + uploadId;
        }

        private void EnsureMetadataTable()
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                var sql = "IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = " + "'" + "RFC_Upload_Metadata" + "'" + ") " +
                          "CREATE TABLE RFC_Upload_Metadata (" +
                          "    Id               INT           IDENTITY(1,1) PRIMARY KEY," +
                          "    ApiName          NVARCHAR(255) NULL," +
                          "    FileName         NVARCHAR(255) NOT NULL," +
                          "    OriginalFileName NVARCHAR(255) NOT NULL," +
                          "    FilePath         NVARCHAR(500) NOT NULL," +
                          "    FileSizeBytes    BIGINT        NOT NULL," +
                          "    ContentType      NVARCHAR(100) NULL," +
                          "    UploadedBy       NVARCHAR(100) NULL," +
                          "    UploadedAt       DATETIME2     NOT NULL DEFAULT GETUTCDATE()," +
                          "    Status           NVARCHAR(50)  NOT NULL DEFAULT " + "'" + "UPLOADED" + "'" + "," +
                          "    Remarks          NVARCHAR(MAX) NULL" +
                          ")";
                using (var cmd = new SqlCommand(sql, conn)) cmd.ExecuteNonQuery();
            }
        }

        private RfcUploadMetadata GetMetadataById(int id)
        {
            const string sql = "SELECT Id, ApiName, FileName, OriginalFileName, FilePath, FileSizeBytes," +
                               "       ContentType, UploadedBy, UploadedAt, Status, Remarks " +
                               "FROM RFC_Upload_Metadata WHERE Id = @Id";
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return null;
                        return MapRow(rdr);
                    }
                }
            }
        }

        private static RfcUploadMetadata MapRow(System.Data.SqlClient.SqlDataReader rdr)
        {
            return new RfcUploadMetadata
            {
                Id               = (int)rdr["Id"],
                ApiName          = rdr["ApiName"]          is DBNull ? null : (string)rdr["ApiName"],
                FileName         = (string)rdr["FileName"],
                OriginalFileName = (string)rdr["OriginalFileName"],
                FilePath         = (string)rdr["FilePath"],
                FileSizeBytes    = (long)rdr["FileSizeBytes"],
                ContentType      = rdr["ContentType"]      is DBNull ? null : (string)rdr["ContentType"],
                UploadedBy       = rdr["UploadedBy"]       is DBNull ? null : (string)rdr["UploadedBy"],
                UploadedAt       = (DateTime)rdr["UploadedAt"],
                Status           = (string)rdr["Status"],
                Remarks          = rdr["Remarks"]          is DBNull ? null : (string)rdr["Remarks"]
            };
        }

        private static string SanitizeForFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "RFC";
            var invalid = new string(Path.GetInvalidFileNameChars());
            var sb = new StringBuilder();
            foreach (var c in input) sb.Append(invalid.IndexOf(c) < 0 ? c : '_');
            return sb.ToString().ToUpperInvariant();
        }

        // 1. Upload
        [HttpPost]
        [Route("Upload")]
        public async Task<HttpResponseMessage> Upload()
        {
            if (!Request.Content.IsMimeMultipartContent())
                return Request.CreateResponse(HttpStatusCode.UnsupportedMediaType,
                    new { success = false, message = "Request must be multipart/form-data." });
            try
            {
                var provider = await Request.Content.ReadAsMultipartAsync(new MultipartMemoryStreamProvider());
                HttpContent filePart = null;
                string apiName = null, uploadedBy = null, originalName = null;
                string contentType = "application/octet-stream";
                foreach (var part in provider.Contents)
                {
                    var disp = part.Headers.ContentDisposition;
                    if (disp == null) continue;
                    var fieldName = (disp.Name ?? "").Trim('"');
                    if (disp.FileName != null)
                    {
                        filePart = part;
                        originalName = disp.FileName.Trim('"');
                        contentType = part.Headers.ContentType?.MediaType ?? contentType;
                    }
                    else if (string.Equals(fieldName, "apiName",    StringComparison.OrdinalIgnoreCase)) apiName    = await part.ReadAsStringAsync();
                    else if (string.Equals(fieldName, "uploadedBy", StringComparison.OrdinalIgnoreCase)) uploadedBy = await part.ReadAsStringAsync();
                }
                if (filePart == null) return Request.CreateResponse(HttpStatusCode.BadRequest, new { success = false, message = "No file in request." });
                if (string.IsNullOrWhiteSpace(apiName)) return Request.CreateResponse(HttpStatusCode.BadRequest, new { success = false, message = "Form field apiName is required." });
                var ext = Path.GetExtension(originalName ?? "").ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { success = false, message = "Invalid file type " + ext + ". Allowed: " + string.Join(", ", AllowedExtensions) });
                var fileBytes = await filePart.ReadAsByteArrayAsync();
                if (fileBytes.Length > MaxFileSizeBytes)
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new { success = false, message = "File exceeds 50 MB limit." });
                var safeName = SanitizeForFileName(apiName) + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext;
                var filePath = Path.Combine(GetUploadRoot(), safeName);
                File.WriteAllBytes(filePath, fileBytes);
                EnsureMetadataTable();
                var insertSql = "INSERT INTO RFC_Upload_Metadata (ApiName,FileName,OriginalFileName,FilePath,FileSizeBytes,ContentType,UploadedBy,Status) OUTPUT INSERTED.Id VALUES (@ApiName,@FileName,@OriginalFileName,@FilePath,@FileSizeBytes,@ContentType,@UploadedBy," + "'" + "UPLOADED" + "'" + ")";
                int newId;
                using (var conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ApiName",          (object)apiName    ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FileName",          safeName);
                        cmd.Parameters.AddWithValue("@OriginalFileName",  (object)originalName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FilePath",          filePath);
                        cmd.Parameters.AddWithValue("@FileSizeBytes",     (long)fileBytes.Length);
                        cmd.Parameters.AddWithValue("@ContentType",       (object)contentType  ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UploadedBy",        (object)uploadedBy   ?? DBNull.Value);
                        newId = (int)cmd.ExecuteScalar();
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new RfcUploadResponse
                {
                    Success = true, Message = "File uploaded successfully.",
                    UploadId = newId, ApiName = apiName, FileName = safeName,
                    FileSizeBytes = fileBytes.Length, UploadedAt = DateTime.UtcNow,
                    Status = "UPLOADED", DownloadUrl = BuildDownloadUrl(newId)
                });
            }
            catch (Exception ex)
            { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { success = false, message = "Upload failed: " + ex.Message }); }
        }

        // 2. Status
        [HttpGet]
        [Route("Status/{uploadId:int}")]
        public HttpResponseMessage GetStatus(int uploadId)
        {
            try
            {
                EnsureMetadataTable();
                var meta = GetMetadataById(uploadId);
                if (meta == null) return Request.CreateResponse(HttpStatusCode.NotFound, new { success = false, message = "Upload ID " + uploadId + " not found." });
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    success = true, message = "Record found.", uploadId = meta.Id,
                    apiName = meta.ApiName, fileName = meta.FileName, originalFileName = meta.OriginalFileName,
                    fileSizeBytes = meta.FileSizeBytes, uploadedBy = meta.UploadedBy,
                    uploadedAt = meta.UploadedAt, status = meta.Status, remarks = meta.Remarks,
                    downloadUrl = BuildDownloadUrl(meta.Id)
                });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { success = false, message = ex.Message }); }
        }

        // 3. List all
        [HttpGet]
        [Route("List")]
        public HttpResponseMessage GetList()
        {
            try
            {
                EnsureMetadataTable();
                var sql = "SELECT Id,ApiName,FileName,OriginalFileName,FilePath,FileSizeBytes,ContentType,UploadedBy,UploadedAt,Status,Remarks FROM RFC_Upload_Metadata ORDER BY UploadedAt DESC";
                var list = new System.Collections.Generic.List<object>();
                using (var conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read()) { var m = MapRow(rdr); list.Add(new { uploadId=m.Id, apiName=m.ApiName, fileName=m.FileName, originalFileName=m.OriginalFileName, fileSizeBytes=m.FileSizeBytes, uploadedBy=m.UploadedBy, uploadedAt=m.UploadedAt, status=m.Status, downloadUrl=BuildDownloadUrl(m.Id) }); }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new { success=true, message=list.Count+" record(s) found.", total=list.Count, documents=list });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { success=false, message=ex.Message }); }
        }

        // 4. List docs for a specific API name
        [HttpGet]
        [Route("Docs/{apiName}")]
        public HttpResponseMessage GetDocsByApi(string apiName)
        {
            if (string.IsNullOrWhiteSpace(apiName))
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { success=false, message="apiName is required." });
            try
            {
                EnsureMetadataTable();
                var sql = "SELECT Id,ApiName,FileName,OriginalFileName,FilePath,FileSizeBytes,ContentType,UploadedBy,UploadedAt,Status,Remarks FROM RFC_Upload_Metadata WHERE ApiName=@ApiName ORDER BY UploadedAt DESC";
                var docs = new System.Collections.Generic.List<RfcDocListItem>();
                using (var conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ApiName", apiName);
                        using (var rdr = cmd.ExecuteReader())
                            while (rdr.Read()) { var m=MapRow(rdr); docs.Add(new RfcDocListItem { UploadId=m.Id, ApiName=m.ApiName, OriginalFileName=m.OriginalFileName, FileSizeBytes=m.FileSizeBytes, ContentType=m.ContentType, UploadedBy=m.UploadedBy, UploadedAt=m.UploadedAt, Status=m.Status, DownloadUrl=BuildDownloadUrl(m.Id) }); }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new RfcDocListResponse { Success=true, Message=docs.Count+" document(s) found for API "+apiName+".", ApiName=apiName, TotalDocs=docs.Count, Documents=docs });
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { success=false, message=ex.Message }); }
        }

        // 5. Download
        [HttpGet]
        [Route("Download/{uploadId:int}")]
        public HttpResponseMessage Download(int uploadId)
        {
            try
            {
                EnsureMetadataTable();
                var meta = GetMetadataById(uploadId);
                if (meta == null) return Request.CreateResponse(HttpStatusCode.NotFound, new { success=false, message="Upload ID "+uploadId+" not found." });
                if (!File.Exists(meta.FilePath)) return Request.CreateResponse(HttpStatusCode.NotFound, new { success=false, message="File not found on disk for upload ID "+uploadId+"." });
                var bytes   = File.ReadAllBytes(meta.FilePath);
                var mime    = string.IsNullOrWhiteSpace(meta.ContentType) ? "application/octet-stream" : meta.ContentType;
                var result  = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                result.Content.Headers.ContentType        = new MediaTypeHeaderValue(mime);
                result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = meta.OriginalFileName ?? meta.FileName };
                result.Content.Headers.ContentLength      = bytes.LongLength;
                return result;
            }
            catch (Exception ex) { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { success=false, message="Download failed: "+ex.Message }); }
        }
    }
}
