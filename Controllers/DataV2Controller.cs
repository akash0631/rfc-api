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
    /// DataV2 SQL Query API — executes any T-SQL against the V2 Retail Data Lake
    /// Secured by x-api-key header. Used by the AI analyst layer at sql.v2retail.net
    /// </summary>
    [RoutePrefix("api/datav2")]
    public class DataV2Controller : ApiController
    {
        private const string API_KEY       = "v2-datav2-analyst-2026";
        private const string CONN_STR      = @"Server=192.168.151.28;Database=DataV2;User Id=sa;Password=vrl@55555;Connection Timeout=60;";
        private const int    MAX_ROWS      = 50000;   // safety cap
        private const int    TIMEOUT_SEC   = 120;

        // ── Auth helper ───────────────────────────────────────────────────────
        private bool Auth() {
            IEnumerable<string> vals;
            if (Request.Headers.TryGetValues("x-api-key", out vals))
                return vals.FirstOrDefault() == API_KEY;
            return false;
        }

        // ── GET /api/datav2/health ────────────────────────────────────────────
        [HttpGet, Route("health")]
        public HttpResponseMessage Health()
        {
            try {
                using var conn = new SqlConnection(CONN_STR);
                conn.Open();
                using var cmd = new SqlCommand("SELECT @@SERVERNAME AS svr, DB_NAME() AS db, GETDATE() AS ts", conn);
                using var rdr = cmd.ExecuteReader();
                rdr.Read();
                var result = new { status="ok", server=rdr["svr"].ToString(),
                    database=rdr["db"].ToString(), timestamp=rdr["ts"].ToString() };
                conn.Close();
                var r = Request.CreateResponse(HttpStatusCode.OK);
                r.Content = new StringContent(JsonConvert.SerializeObject(result),
                    Encoding.UTF8, "application/json");
                r.Headers.Add("Access-Control-Allow-Origin","*");
                return r;
            } catch (Exception ex) {
                var r = Request.CreateResponse(HttpStatusCode.ServiceUnavailable);
                r.Content = new StringContent(JsonConvert.SerializeObject(
                    new { status="error", message=ex.Message }), Encoding.UTF8, "application/json");
                r.Headers.Add("Access-Control-Allow-Origin","*");
                return r;
            }
        }

        // ── POST /api/datav2/query ────────────────────────────────────────────
        // Body: { "sql": "SELECT TOP 100 ...", "params": {} }
        [HttpPost, Route("query")]
        public async Task<HttpResponseMessage> Query()
        {
            if (!Auth()) return Unauthorized();

            string body = await Request.Content.ReadAsStringAsync();
            JObject req;
            try { req = JObject.Parse(body); }
            catch { return BadReq("Invalid JSON body"); }

            string sql = req["sql"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(sql)) return BadReq("sql field required");

            // Block destructive statements
            var blocked = new[]{"DROP ","DELETE ","TRUNCATE ","ALTER ","CREATE ","INSERT ","UPDATE ","EXEC ","EXECUTE ","GRANT ","REVOKE "};
            string sqlUpper = sql.ToUpper();
            foreach (var b in blocked)
                if (sqlUpper.Contains(b)) return BadReq($"Blocked keyword: {b.Trim()}. Only SELECT queries allowed.");

            // Auto-add TOP safety limit if not present
            if (!sqlUpper.Contains("TOP ") && !sqlUpper.Contains("LIMIT "))
                sql = sql.Replace("SELECT ", $"SELECT TOP {MAX_ROWS} ", StringComparison.OrdinalIgnoreCase);

            try {
                var rows   = new List<Dictionary<string,object>>();
                var cols   = new List<string>();
                var started= DateTime.UtcNow;

                using var conn = new SqlConnection(CONN_STR);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout=TIMEOUT_SEC };
                using var rdr = await cmd.ExecuteReaderAsync();

                // Column names
                for (int i=0; i<rdr.FieldCount; i++) cols.Add(rdr.GetName(i));

                // Rows
                int rowCount = 0;
                while (await rdr.ReadAsync() && rowCount < MAX_ROWS) {
                    var row = new Dictionary<string,object>();
                    for (int i=0; i<rdr.FieldCount; i++) {
                        var val = rdr[i];
                        row[cols[i]] = val is DBNull ? null : val;
                    }
                    rows.Add(row);
                    rowCount++;
                }

                var ms = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                var result = new {
                    success  = true,
                    rows     = rowCount,
                    columns  = cols,
                    data     = rows,
                    ms       = ms,
                    truncated= rowCount == MAX_ROWS,
                    sql      = sql
                };

                conn.Close();
                var resp = Request.CreateResponse(HttpStatusCode.OK);
                resp.Content = new StringContent(JsonConvert.SerializeObject(result),
                    Encoding.UTF8, "application/json");
                resp.Headers.Add("Access-Control-Allow-Origin","*");
                return resp;

            } catch (Exception ex) {
                return SqlError(ex.Message, sql);
            }
        }

        // ── GET /api/datav2/tables ────────────────────────────────────────────
        [HttpGet, Route("tables")]
        public async Task<HttpResponseMessage> Tables([FromUri]string search="")
        {
            if (!Auth()) return Unauthorized();
            string filter = string.IsNullOrEmpty(search) ? "" :
                $"WHERE TABLE_NAME LIKE '%{search.Replace("'","")}%'";
            string sql = $@"SELECT TABLE_NAME, 
                (SELECT SUM(p.rows) FROM sys.partitions p 
                 JOIN sys.objects o ON p.object_id=o.object_id 
                 WHERE o.name=t.TABLE_NAME AND p.index_id<2) AS ROW_COUNT
                FROM INFORMATION_SCHEMA.TABLES t
                WHERE TABLE_TYPE=''BASE TABLE'' {(string.IsNullOrEmpty(search)?"":" AND TABLE_NAME LIKE ''%"+search.Replace("'","")+"%''")}
                ORDER BY ROW_COUNT DESC";
            return await RunReadOnly(sql);
        }

        // ── GET /api/datav2/schema/{table} ───────────────────────────────────
        [HttpGet, Route("schema/{table}")]
        public async Task<HttpResponseMessage> Schema(string table)
        {
            if (!Auth()) return Unauthorized();
            string safe = table.Replace("'","").Replace(";","");
            string sql  = $@"SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, 
                IS_NULLABLE, COLUMN_DEFAULT
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME='{safe}' ORDER BY ORDINAL_POSITION";
            return await RunReadOnly(sql);
        }

        // ── POST /api/datav2/execute-write ───────────────────────────────────
        // For CREATE VIEW, CREATE TABLE, INSERT (admin only — separate key)
        [HttpPost, Route("execute-write")]
        public async Task<HttpResponseMessage> ExecuteWrite()
        {
            IEnumerable<string> vals;
            bool adminAuth = Request.Headers.TryGetValues("x-admin-key", out vals) &&
                             vals.FirstOrDefault() == "v2-datav2-admin-2026";
            if (!adminAuth) return Unauthorized();

            string body = await Request.Content.ReadAsStringAsync();
            JObject req; try { req = JObject.Parse(body); } catch { return BadReq("Invalid JSON"); }
            string sql = req["sql"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(sql)) return BadReq("sql required");

            try {
                using var conn = new SqlConnection(CONN_STR);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout=TIMEOUT_SEC };
                int affected = await cmd.ExecuteNonQueryAsync();
                conn.Close();
                var resp = Request.CreateResponse(HttpStatusCode.OK);
                resp.Content = new StringContent(JsonConvert.SerializeObject(
                    new {success=true, rows_affected=affected, sql}), Encoding.UTF8, "application/json");
                resp.Headers.Add("Access-Control-Allow-Origin","*");
                return resp;
            } catch (Exception ex) { return SqlError(ex.Message, sql); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private async Task<HttpResponseMessage> RunReadOnly(string sql) {
            try {
                using var conn = new SqlConnection(CONN_STR);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn){CommandTimeout=TIMEOUT_SEC};
                using var rdr = await cmd.ExecuteReaderAsync();
                var cols = Enumerable.Range(0,rdr.FieldCount).Select(i=>rdr.GetName(i)).ToList();
                var rows = new List<Dictionary<string,object>>();
                while(await rdr.ReadAsync()){
                    var row=new Dictionary<string,object>();
                    for(int i=0;i<rdr.FieldCount;i++) row[cols[i]]=rdr[i] is DBNull?null:rdr[i];
                    rows.Add(row);
                }
                conn.Close();
                var r=Request.CreateResponse(HttpStatusCode.OK);
                r.Content=new StringContent(JsonConvert.SerializeObject(
                    new{success=true,rows=rows.Count,columns=cols,data=rows}),Encoding.UTF8,"application/json");
                r.Headers.Add("Access-Control-Allow-Origin","*");
                return r;
            } catch(Exception ex){return SqlError(ex.Message,sql);}
        }

        private HttpResponseMessage Unauthorized(){
            var r=Request.CreateResponse(HttpStatusCode.Unauthorized);
            r.Content=new StringContent(JsonConvert.SerializeObject(
                new{error="Unauthorized — provide x-api-key header"}),Encoding.UTF8,"application/json");
            r.Headers.Add("Access-Control-Allow-Origin","*");
            return r;
        }
        private HttpResponseMessage BadReq(string msg){
            var r=Request.CreateResponse(HttpStatusCode.BadRequest);
            r.Content=new StringContent(JsonConvert.SerializeObject(
                new{error=msg}),Encoding.UTF8,"application/json");
            r.Headers.Add("Access-Control-Allow-Origin","*");
            return r;
        }
        private HttpResponseMessage SqlError(string msg, string sql){
            var r=Request.CreateResponse(HttpStatusCode.InternalServerError);
            r.Content=new StringContent(JsonConvert.SerializeObject(
                new{error=msg,sql}),Encoding.UTF8,"application/json");
            r.Headers.Add("Access-Control-Allow-Origin","*");
            return r;
        }
    }
}
