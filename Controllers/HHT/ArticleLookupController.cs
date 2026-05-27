using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vendor_Application_MVC.Controllers.HHT
{
    // BRD: V09 -> 0001 Article Putaway HHT screen.
    // POST /api/article-lookup  body: { "store":"HA11", "article":"1125011967001" }
    // Returns: { status, article_type: L|RL|NL|C, article_size: BS|S|NORMAL, ... }
    public class ArticleLookupController : ApiController
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain,
            string lpszPassword, int dwLogonType, int dwLogonProvider, out IntPtr phToken);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        private const string WIN_DOMAIN = "V2RD";
        private const string WIN_USER   = "akash.agarwal";
        private const string WIN_PASS   = "vrl@99999";
        private static readonly string CS_INTEGRATED =
            @"Server=192.168.151.28;Database=DataV2;Integrated Security=True;Connection Timeout=5;MultipleActiveResultSets=true;";
        private static readonly string CS_FALLBACK =
            @"Server=192.168.151.28;Database=DataV2;User Id=V2ApiReader;Password=V2Api@2026;Connection Timeout=5;MultipleActiveResultSets=true;";

        private SqlConnection GetConnection()
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                bool ok = LogonUser(WIN_USER, WIN_DOMAIN, WIN_PASS, 9, 0, out token);
                if (ok && token != IntPtr.Zero)
                {
                    var wi = new WindowsIdentity(token);
                    SqlConnection conn = null;
                    WindowsIdentity.RunImpersonated(wi.AccessToken, () => {
                        try { conn = new SqlConnection(CS_INTEGRATED); conn.Open(); }
                        catch { conn?.Dispose(); conn = null; }
                    });
                    if (conn != null && conn.State == ConnectionState.Open) return conn;
                }
            }
            catch { }
            finally { if (token != IntPtr.Zero) CloseHandle(token); }

            try { var c = new SqlConnection(CS_INTEGRATED); c.Open(); return c; } catch { }
            try { var c = new SqlConnection(CS_FALLBACK); c.Open(); return c; } catch { }
            throw new Exception("DataV2 (Server 28) connection failed");
        }

        private HttpResponseMessage Json(HttpStatusCode code, object body)
        {
            var r = Request.CreateResponse(code);
            r.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            r.Headers.Add("Access-Control-Allow-Origin", "*");
            return r;
        }

        [HttpOptions, Route("api/article-lookup")]
        public HttpResponseMessage Options()
        {
            var r = Request.CreateResponse(HttpStatusCode.OK);
            r.Headers.Add("Access-Control-Allow-Origin", "*");
            r.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            r.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            return r;
        }

        [HttpPost, Route("api/article-lookup")]
        public async Task<HttpResponseMessage> Lookup()
        {
            var t0 = DateTime.UtcNow;
            string body = await Request.Content.ReadAsStringAsync();
            JObject req;
            try { req = JObject.Parse(body ?? ""); }
            catch { return Json(HttpStatusCode.BadRequest, new { status = false, message = "Invalid JSON body" }); }

            string store   = (req["store"]   ?? req["STORE"]   ?? req["IM_STORE"])?.ToString()?.Trim();
            string article = (req["article"] ?? req["ARTICLE"] ?? req["ARTICLE_NUMBER"] ?? req["IM_ARTICLE"])?.ToString()?.Trim();

            if (string.IsNullOrEmpty(store) || string.IsNullOrEmpty(article))
                return Json(HttpStatusCode.BadRequest, new { status = false, message = "store and article are required" });

            long artNum;
            if (!long.TryParse(article, out artNum))
                return Json(HttpStatusCode.BadRequest, new { status = false, message = "article must be numeric (variant article number)" });

            // BRD rule:
            //   article_type = 'C'  if active discount row exists in ST_ART_DISCOUNT for (store|All, article, today)
            //                 else L_VAR_ARTICLE.L_STATUS (L/RL/NL) for (store, article)
            //                 else 'NL'
            //   article_size = SZ_GROUP_VAR_ARTICLE.SIZE_GRP (BS/S) for article; 'N' -> 'NORMAL'
            const string sql = @"
DECLARE @today DATE = CAST(GETDATE() AS DATE);
SELECT
  CASE
    WHEN EXISTS (
      SELECT 1 FROM dbo.ST_ART_DISCOUNT WITH(NOLOCK)
       WHERE MATNR = @art
         AND (ST_CD = @store OR ST_CD = 'All')
         AND @today BETWEEN VALID_FROM_DT AND VALID_TO_DT
    ) THEN 'C'
    ELSE ISNULL(
      (SELECT TOP 1 L_STATUS FROM dbo.L_VAR_ARTICLE WITH(NOLOCK)
        WHERE STORE = @store AND ARTICLE_NUMBER = @art),
      'NL')
  END                                                AS article_type,
  ISNULL(
    (SELECT TOP 1 SIZE_GRP FROM dbo.SZ_GROUP_VAR_ARTICLE WITH(NOLOCK)
      WHERE ARTICLE_NUMBER = @art),
    'N')                                             AS size_grp_raw;";

            try
            {
                string typeVal = "NL";
                string sizeRaw = "N";
                using (var conn = GetConnection())
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 5 })
                {
                    cmd.Parameters.Add(new SqlParameter("@store", SqlDbType.NVarChar, 50) { Value = store });
                    cmd.Parameters.Add(new SqlParameter("@art",   SqlDbType.BigInt)        { Value = artNum });
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        if (await rd.ReadAsync())
                        {
                            typeVal = rd["article_type"]  is DBNull ? "NL" : rd["article_type"].ToString();
                            sizeRaw = rd["size_grp_raw"] is DBNull ? "N"  : rd["size_grp_raw"].ToString();
                        }
                    }
                }

                string sizeVal = sizeRaw == "BS" ? "BS"
                              :  sizeRaw == "S"  ? "S"
                              :                    "NORMAL";

                return Json(HttpStatusCode.OK, new {
                    status        = true,
                    store         = store,
                    article       = article,
                    article_type  = typeVal,
                    article_size  = sizeVal,
                    ms            = (int)(DateTime.UtcNow - t0).TotalMilliseconds
                });
            }
            catch (Exception ex)
            {
                // Per BRD 5.4: on lookup failure, return placeholder + allow operator to continue.
                return Json(HttpStatusCode.OK, new {
                    status       = false,
                    store        = store,
                    article      = article,
                    article_type = (string)null,
                    article_size = (string)null,
                    message      = "Lookup unavailable: " + ex.Message,
                    ms           = (int)(DateTime.UtcNow - t0).TotalMilliseconds
                });
            }
        }
    }
}
