using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vendor_SRM_Routing_Application.Controllers
{
    /// <summary>
    /// DataV2 SQL Query API — executes T-SQL against V2 Retail Data Lake (Server 28)
    /// </summary>
    public class DataV2Controller : ApiController
    {
        private const string API_KEY    = "v2-datav2-analyst-2026";
        private const string ADMIN_KEY  = "v2-datav2-admin-2026";
        private const string CONN_STR   = @"Server=192.168.151.28;Database=DataV2;User Id=sa;Password=vrl@55555;Connection Timeout=60;MultipleActiveResultSets=true;";
        private const int    MAX_ROWS   = 50000;
        private const int    TIMEOUT    = 120;

        private bool AuthRead()  {
            IEnumerable<string> v;
            return Request.Headers.TryGetValues("x-api-key", out v) && v.FirstOrDefault() == API_KEY;
        }
        private bool AuthWrite() {
            IEnumerable<string> v;
            return Request.Headers.TryGetValues("x-admin-key", out v) && v.FirstOrDefault() == ADMIN_KEY;
        }

        private HttpResponseMessage Ok(object obj) {
            var r = Request.CreateResponse(HttpStatusCode.OK);
            r.Content = new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");
            r.Headers.Add("Access-Control-Allow-Origin", "*");
            return r;
        }
        private HttpResponseMessage Fail(string msg, HttpStatusCode code = HttpStatusCode.BadRequest) {
            var r = Request.CreateResponse(code);
            r.Content = new StringContent(JsonConvert.SerializeObject(new { error = msg }), Encoding.UTF8, "application/json");
            r.Headers.Add("Access-Control-Allow-Origin", "*");
            return r;
        }

        // OPTIONS preflight
        [HttpOptions, Route("api/datav2/{*path}")]
        public HttpResponseMessage Options() {
            var r = Request.CreateResponse(HttpStatusCode.OK);
            r.Headers.Add("Access-Control-Allow-Origin", "*");
            r.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            r.Headers.Add("Access-Control-Allow-Headers", "Content-Type, x-api-key, x-admin-key");
            return r;
        }

        // GET /api/datav2/health
        [HttpGet, Route("api/datav2/health")]
        public HttpResponseMessage Health() {
            try {
                using (var conn = new SqlConnection(CONN_STR)) {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT @@SERVERNAME svr, DB_NAME() db, GETDATE() ts, (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE') tbl_count", conn)) {
                        using (var rd = cmd.ExecuteReader()) {
                            rd.Read();
                            return Ok(new { status = "ok", server = rd["svr"].ToString(),
                                database = rd["db"].ToString(), timestamp = rd["ts"].ToString(),
                                table_count = rd["tbl_count"].ToString() });
                        }
                    }
                }
            } catch (Exception ex) { return Fail("DB error: " + ex.Message, HttpStatusCode.ServiceUnavailable); }
        }

        // POST /api/datav2/query  — execute any SELECT T-SQL
        [HttpPost, Route("api/datav2/query")]
        public async Task<HttpResponseMessage> Query() {
            if (!AuthRead()) return Fail("Unauthorized — provide x-api-key", HttpStatusCode.Unauthorized);
            string body = await Request.Content.ReadAsStringAsync();
            JObject req; try { req = JObject.Parse(body); } catch { return Fail("Invalid JSON"); }
            string sql = req["sql"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(sql)) return Fail("sql field required");

            // Block write operations
            var blocked = new[] { "DROP ", "DELETE ", "TRUNCATE ", "ALTER TABLE", "CREATE TABLE",
                "INSERT INTO", "UPDATE ", "EXEC ", "EXECUTE ", "GRANT ", "REVOKE ", "MERGE " };
            string up = sql.ToUpperInvariant();
            foreach (var b in blocked)
                if (up.Contains(b)) return Fail("Blocked keyword: " + b.Trim() + ". Use /execute-write for DDL.");

            // Add TOP safety cap if missing
            if (!up.Contains("TOP ") && up.StartsWith("SELECT"))
                sql = "SELECT TOP " + MAX_ROWS + " " + sql.Substring(6).TrimStart();

            try {
                var rows = new List<Dictionary<string, object>>();
                var cols = new List<string>();
                var t0 = DateTime.UtcNow;
                using (var conn = new SqlConnection(CONN_STR)) {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = TIMEOUT }) {
                        using (var rd = await cmd.ExecuteReaderAsync()) {
                            for (int i = 0; i < rd.FieldCount; i++) cols.Add(rd.GetName(i));
                            int n = 0;
                            while (await rd.ReadAsync() && n < MAX_ROWS) {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < rd.FieldCount; i++)
                                    row[cols[i]] = rd[i] is DBNull ? null : rd[i];
                                rows.Add(row); n++;
                            }
                        }
                    }
                }
                return Ok(new { success = true, rows = rows.Count, columns = cols, data = rows,
                    ms = (int)(DateTime.UtcNow - t0).TotalMilliseconds,
                    truncated = rows.Count == MAX_ROWS, sql });
            } catch (Exception ex) {
                return Ok(new { success = false, error = ex.Message, sql });
            }
        }

        // GET /api/datav2/tables?search=SALE
        [HttpGet, Route("api/datav2/tables")]
        public async Task<HttpResponseMessage> Tables([FromUri] string search = "") {
            if (!AuthRead()) return Fail("Unauthorized", HttpStatusCode.Unauthorized);
            string where = string.IsNullOrEmpty(search) ? "" : " AND t.TABLE_NAME LIKE '%" + search.Replace("'","") + "%'";
            string sql = @"SELECT t.TABLE_NAME,
                ISNULL((SELECT SUM(p.rows) FROM sys.partitions p JOIN sys.objects o ON p.object_id=o.object_id
                 WHERE o.name=t.TABLE_NAME AND p.index_id<2),0) AS ROW_COUNT
                FROM INFORMATION_SCHEMA.TABLES t WHERE t.TABLE_TYPE='BASE TABLE'" + where + " ORDER BY ROW_COUNT DESC";
            try {
                var rows = new List<object>(); var t0 = DateTime.UtcNow;
                using (var conn = new SqlConnection(CONN_STR)) {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn){CommandTimeout=60})
                    using (var rd = await cmd.ExecuteReaderAsync())
                        while(await rd.ReadAsync())
                            rows.Add(new{table_name=rd["TABLE_NAME"].ToString(),row_count=rd["ROW_COUNT"]});
                }
                return Ok(new{success=true,tables=rows.Count,data=rows,ms=(int)(DateTime.UtcNow-t0).TotalMilliseconds});
            } catch(Exception ex){return Fail(ex.Message,HttpStatusCode.InternalServerError);}
        }

        // GET /api/datav2/schema/{table}
        [HttpGet, Route("api/datav2/schema/{table}")]
        public async Task<HttpResponseMessage> Schema(string table) {
            if (!AuthRead()) return Fail("Unauthorized", HttpStatusCode.Unauthorized);
            string safe = table.Replace("'","").Replace(";","").Replace("--","");
            string sql = "SELECT COLUMN_NAME,DATA_TYPE,CHARACTER_MAXIMUM_LENGTH,IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='" + safe + "' ORDER BY ORDINAL_POSITION";
            try {
                var rows = new List<object>();
                using(var conn=new SqlConnection(CONN_STR)){
                    await conn.OpenAsync();
                    using(var cmd=new SqlCommand(sql,conn){CommandTimeout=30})
                    using(var rd=await cmd.ExecuteReaderAsync())
                        while(await rd.ReadAsync())
                            rows.Add(new{col=rd["COLUMN_NAME"].ToString(),type=rd["DATA_TYPE"].ToString(),
                                max_len=rd["CHARACTER_MAXIMUM_LENGTH"]is DBNull?null:(object)rd["CHARACTER_MAXIMUM_LENGTH"],
                                nullable=rd["IS_NULLABLE"].ToString()});
                }
                return Ok(new{success=true,table=safe,columns=rows.Count,data=rows});
            } catch(Exception ex){return Fail(ex.Message,HttpStatusCode.InternalServerError);}
        }

        // POST /api/datav2/execute-write — DDL/DML (admin key required)
        [HttpPost, Route("api/datav2/execute-write")]
        public async Task<HttpResponseMessage> ExecuteWrite() {
            if (!AuthWrite()) return Fail("Admin key required (x-admin-key)", HttpStatusCode.Unauthorized);
            string body = await Request.Content.ReadAsStringAsync();
            JObject req; try{req=JObject.Parse(body);}catch{return Fail("Invalid JSON");}
            string sql = req["sql"]?.ToString()?.Trim();
            if(string.IsNullOrEmpty(sql)) return Fail("sql required");
            try {
                using(var conn=new SqlConnection(CONN_STR)){
                    await conn.OpenAsync();
                    using(var cmd=new SqlCommand(sql,conn){CommandTimeout=TIMEOUT}){
                        int n = await cmd.ExecuteNonQueryAsync();
                        return Ok(new{success=true,rows_affected=n,sql});
                    }
                }
            } catch(Exception ex){return Ok(new{success=false,error=ex.Message,sql});}
        }
    }
}
