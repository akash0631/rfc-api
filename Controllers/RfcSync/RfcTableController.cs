using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.RfcSync;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// C — MSSQL Table Management API
    /// Creates destination table dynamically if not exists.
    /// Uses parameterised SQL wherever possible; table name is whitelist-validated.
    /// Route: POST /api/RfcTable/Create
    ///        GET  /api/RfcTable/Exists/{tableName}
    /// </summary>
    [RoutePrefix("api/RfcTable")]
    public class RfcTableController : BaseController
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

        // Table name must be alphanumeric + underscore only (SQL injection guard)
        private static readonly Regex SafeTableName = new Regex("^[A-Za-z0-9_]{1,128}$");

        // ── Create Table If Not Exists ─────────────────────────────────────
        [HttpPost]
        [Route("Create")]
        public HttpResponseMessage Create([FromBody] RfcTableRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TableName))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new RfcTableResponse { Success = false, Message = "TableName is required." });

            if (!SafeTableName.IsMatch(request.TableName))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new RfcTableResponse { Success = false, Message = "TableName contains invalid characters. Use A-Z, 0-9, _ only." });

            try
            {
                bool existed = TableExists(request.TableName);
                if (!existed)
                    CreateRfcDataTable(request.TableName);

                return Request.CreateResponse(HttpStatusCode.OK, new RfcTableResponse
                {
                    Success        = true,
                    TableName      = request.TableName,
                    AlreadyExisted = existed,
                    Created        = !existed,
                    Message        = existed ? "Table already exists." : "Table created successfully."
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new RfcTableResponse { Success = false, Message = "Error: " + ex.Message });
            }
        }

        // ── Check Table Existence ────────────────────────────────────────────
        [HttpGet]
        [Route("Exists/{tableName}")]
        public HttpResponseMessage Exists(string tableName)
        {
            if (!SafeTableName.IsMatch(tableName ?? string.Empty))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Success = false, Message = "Invalid table name." });

            try
            {
                bool exists = TableExists(tableName);
                return Request.CreateResponse(HttpStatusCode.OK,
                    new { Success = true, TableName = tableName, Exists = exists });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Success = false, Message = "Error: " + ex.Message });
            }
        }

        // ── Public static helper (reused by RfcSyncController) ───────────────
        public static bool TableExists(string tableName)
        {
            string connStr = ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                // Parameterised check against sys.tables — table name as data, not code
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(1) FROM sys.tables WHERE name = @TableName AND schema_id = SCHEMA_ID('dbo')", conn))
                {
                    cmd.Parameters.AddWithValue("@TableName", tableName);
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        public static void CreateRfcDataTable(string tableName)
        {
            if (!SafeTableName.IsMatch(tableName))
                throw new ArgumentException("Invalid table name: " + tableName);

            string connStr = ConfigurationManager.ConnectionStrings["RfcSync"] != null
                ? ConfigurationManager.ConnectionStrings["RfcSync"].ConnectionString
                : ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

            // Table name used in DDL after strict whitelist validation above
            string ddl = "CREATE TABLE [dbo].[" + tableName + "] (" +
                "[Id]             INT            IDENTITY(1,1) NOT NULL PRIMARY KEY," +
                "[RecordDate]     DATE           NOT NULL," +
                "[RfcName]        NVARCHAR(200)  NULL," +
                "[FunctionModule] NVARCHAR(200)  NULL," +
                "[Description]    NVARCHAR(1000) NULL," +
                "[Status]         NVARCHAR(100)  NULL," +
                "[Category]       NVARCHAR(200)  NULL," +
                "[Parameters]     NVARCHAR(MAX)  NULL," +
                "[CreatedBy]      NVARCHAR(200)  NULL," +
                "[Remarks]        NVARCHAR(MAX)  NULL," +
                "[SyncedAt]       DATETIME       NOT NULL DEFAULT GETDATE()" +
            ");";

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var cmd = new SqlCommand(ddl, conn))
                    cmd.ExecuteNonQuery();
            }
        }
    }
}
