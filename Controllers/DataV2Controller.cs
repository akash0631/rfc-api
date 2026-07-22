using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
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

        // Windows impersonation to connect as V2RD\akash.agarwal
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain,
            string lpszPassword, int dwLogonType, int dwLogonProvider, out IntPtr phToken);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        private const string SQL_SERVER = "192.168.151.28";
        private const string SQL_DB     = "DataV2";
        private const string WIN_DOMAIN = "V2RD";
        private const string WIN_USER   = "akash.agarwal";
        private const string WIN_PASS   = "vrl@99999";

        // Pooling is explicit here on purpose. ADO.NET keys its connection pool on
        // (connection string + identity). The old code called LogonUser() per request,
        // which minted a NEW token every time, so every request landed in its own pool
        // bucket and paid a full domain logon + SQL handshake. Measured on .36:
        //   per-call logon : median 45-86 ms, p95 1079 ms at 4 concurrent
        //   cached  logon  : median  1 ms,    p95   16 ms at 8 concurrent
        // Caching the token below is what lets the pool actually do its job.
        private static readonly string CS_INTEGRATED =
            @"Server=192.168.151.28;Database=DataV2;Integrated Security=True;Connection Timeout=15;MultipleActiveResultSets=true;Pooling=true;Min Pool Size=2;Max Pool Size=50;";
        private static readonly string CS_FALLBACK =
            @"Server=192.168.151.28;Database=DataV2;User Id=V2ApiReader;Password=V2Api@2026;Connection Timeout=15;MultipleActiveResultSets=true;Pooling=true;Min Pool Size=2;Max Pool Size=50;";

        // Impersonation token, minted once and reused. Guarded by _identityLock.
        // A LOGON32_LOGON_NEW_CREDENTIALS token does not expire on its own, but it does
        // become useless if WIN_PASS is rotated - InvalidateIdentity() handles that case.
        private static WindowsIdentity _cachedIdentity;
        private static IntPtr _cachedToken = IntPtr.Zero;
        private static readonly object _identityLock = new object();

        private static WindowsIdentity GetImpersonationIdentity()
        {
            var id = _cachedIdentity;
            if (id != null) return id;

            lock (_identityLock)
            {
                if (_cachedIdentity != null) return _cachedIdentity;

                IntPtr token;
                bool ok = LogonUser(WIN_USER, WIN_DOMAIN, WIN_PASS, 9, 0, out token); // LOGON32_LOGON_NEW_CREDENTIALS = network creds for remote SQL
                if (!ok || token == IntPtr.Zero)
                    throw new Exception("LogonUser failed for " + WIN_DOMAIN + "\\" + WIN_USER +
                                        " (win32=" + Marshal.GetLastWin32Error() + ")");

                _cachedToken   = token;
                _cachedIdentity = new WindowsIdentity(token);
                return _cachedIdentity;
            }
        }

        // Drop the cached token so the next call mints a fresh one. Called when a
        // connection attempt fails under the cached identity (e.g. password rotated).
        private static void InvalidateIdentity()
        {
            lock (_identityLock)
            {
                if (_cachedIdentity != null) { try { _cachedIdentity.Dispose(); } catch { } }
                _cachedIdentity = null;
                if (_cachedToken != IntPtr.Zero) { CloseHandle(_cachedToken); _cachedToken = IntPtr.Zero; }
            }
        }

        private static SqlConnection OpenImpersonated()
        {
            var wi = GetImpersonationIdentity();
            SqlConnection conn = null;
            Exception inner = null;
            WindowsIdentity.RunImpersonated(wi.AccessToken, () => {
                try
                {
                    conn = new SqlConnection(CS_INTEGRATED);
                    conn.Open();
                }
                catch (Exception ex) { inner = ex; conn?.Dispose(); conn = null; }
            });
            if (conn != null && conn.State == ConnectionState.Open) return conn;
            throw inner ?? new Exception("Impersonated connection did not open");
        }

        // Returns an ALREADY OPEN connection — callers must NOT call conn.Open() again
        private SqlConnection GetConnection()
        {
            // Strategy 1: cached impersonation of V2RD\akash.agarwal + Integrated Security.
            // Retried once with a fresh token in case the cached one went stale.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try { return OpenImpersonated(); }
                catch
                {
                    InvalidateIdentity();   // force a new logon on the retry
                    if (attempt == 1) break;
                }
            }

            // Strategy 2: Integrated Security without impersonation (if pool runs as domain account)
            try { var c = new SqlConnection(CS_INTEGRATED); c.Open(); return c; } catch { }

            // Strategy 3: SQL login fallback
            // NOTE 2026-07-22: V2ApiReader currently fails with "Login failed for user".
            // This strategy is dead until the account is fixed - see ops handover.
            try { var c = new SqlConnection(CS_FALLBACK); c.Open(); return c; } catch { }

            throw new Exception("All connection strings failed for Server 28");
        }
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
                using (var conn = GetConnection()) {
                    // sys.tables instead of INFORMATION_SCHEMA.TABLES: the latter is a view
                    // with per-row permission checks and cost ~440 ms on this 1406-table DB.
                    // Verified 2026-07-22 that both return the same count (1406).
                    using (var cmd = new SqlCommand("SELECT @@SERVERNAME svr, DB_NAME() db, GETDATE() ts, (SELECT COUNT(*) FROM sys.tables) tbl_count", conn)) {
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
                using (var conn = GetConnection()) {
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
                using (var conn = GetConnection()) {
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
                using(var conn=GetConnection()){
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
                using(var conn=GetConnection()){
                    using(var cmd=new SqlCommand(sql,conn){CommandTimeout=TIMEOUT}){
                        int n = await cmd.ExecuteNonQueryAsync();
                        return Ok(new{success=true,rows_affected=n,sql});
                    }
                }
            } catch(Exception ex){return Ok(new{success=false,error=ex.Message,sql});}
        }
    }
}
