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
using System.Web.Http.Description;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vendor_Application_MVC.Controllers.HHT
{
    /// <summary>Request body for POST /api/article-lookup.</summary>
    public class ArticleLookupRequest
    {
        /// <summary>Store code, e.g. "HA11".</summary>
        [JsonProperty("store")] public string Store { get; set; }
        /// <summary>13-digit variant article number (numeric).</summary>
        [JsonProperty("article")] public string Article { get; set; }
    }

    /// <summary>Response from POST /api/article-lookup. On miss: status=false + null type/size (HHT continues putaway per BRD §5.4).</summary>
    public class ArticleLookupResponse
    {
        [JsonProperty("status")]        public bool   Status       { get; set; }
        [JsonProperty("store")]         public string Store        { get; set; }
        [JsonProperty("article")]       public string Article      { get; set; }
        /// <summary>L | RL | NL | C (null on miss).</summary>
        [JsonProperty("article_type")]  public string ArticleType  { get; set; }
        /// <summary>BS | S | NORMAL (null on miss).</summary>
        [JsonProperty("article_size")]  public string ArticleSize  { get; set; }
        [JsonProperty("source")]        public string Source       { get; set; }
        [JsonProperty("cache_load_ms")] public int    CacheLoadMs  { get; set; }
        [JsonProperty("ms")]            public int    Ms           { get; set; }
        [JsonProperty("message")]       public string Message      { get; set; }
    }

    public class ArticleLookupWarmRequest
    {
        /// <summary>Store code to pre-load into RAM cache.</summary>
        [JsonProperty("store")] public string Store { get; set; }
    }

    public class ArticleLookupWarmResponse
    {
        [JsonProperty("warmed")]  public string Warmed { get; set; }
        [JsonProperty("load_ms")] public int    LoadMs { get; set; }
        [JsonProperty("error")]   public string Error  { get; set; }
        [JsonProperty("status")]  public object Status { get; set; }
    }

    public class ArticleLookupInvalidateRequest
    {
        /// <summary>Optional. Omit to invalidate all cached stores.</summary>
        [JsonProperty("store")] public string Store { get; set; }
    }

    public class ArticleLookupInvalidateResponse
    {
        [JsonProperty("invalidated")] public string Invalidated { get; set; }
        [JsonProperty("status")]      public object Status      { get; set; }
    }

    /// <summary>HHT Article Putaway lookup (V09 → 0001). Returns article_type + article_size for a (store, article) pair.</summary>
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

        /// <summary>CORS preflight.</summary>
        [HttpOptions, Route("api/article-lookup")]
        public HttpResponseMessage Options()
        {
            var r = Request.CreateResponse(HttpStatusCode.OK);
            r.Headers.Add("Access-Control-Allow-Origin", "*");
            r.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            r.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            return r;
        }

        /// <summary>Cache stats: per-store entries, ages, row counts, discount-set size.</summary>
        [HttpGet, Route("api/article-lookup/status")]
        public HttpResponseMessage Status() => Json(HttpStatusCode.OK, ArticleLookupCache.Status());

        /// <summary>Pre-load a store into RAM cache. First scan after warm is &lt;10 ms.</summary>
        [HttpPost, Route("api/article-lookup/warm")]
        [ResponseType(typeof(ArticleLookupWarmResponse))]
        public HttpResponseMessage Warm([FromBody] ArticleLookupWarmRequest req)
        {
            string store = req?.Store?.Trim();
            if (string.IsNullOrEmpty(store))
                return Json(HttpStatusCode.BadRequest, new { status = false, message = "store required for warm" });
            try {
                var (t, s, _, ms) = ArticleLookupCache.Get(store, 0);
                return Json(HttpStatusCode.OK, new { warmed = store, load_ms = ms, status = ArticleLookupCache.Status() });
            } catch (Exception ex) {
                return Json(HttpStatusCode.OK, new { warmed = store, error = ex.Message, status = ArticleLookupCache.Status() });
            }
        }

        /// <summary>
        /// Look up article_type (L|RL|NL|C) and article_size (BS|S|NORMAL) for a (store, article) pair.
        /// Cold first-call per store: 2-5 s (cache load from DataV2 Server 28).
        /// Warm: &lt;10 ms (60-min TTL).
        /// Failure mode: HTTP 200 + status=false + null fields (HHT continues putaway per BRD §5.4).
        /// </summary>
        [HttpPost, Route("api/article-lookup")]
        [ResponseType(typeof(ArticleLookupResponse))]
        public HttpResponseMessage Lookup([FromBody] ArticleLookupRequest req)
        {
            var t0 = DateTime.UtcNow;
            if (req == null)
                return Json(HttpStatusCode.BadRequest, new { status = false, message = "Invalid JSON body" });

            string store   = req.Store?.Trim();
            string article = req.Article?.Trim();

            if (string.IsNullOrEmpty(store) || string.IsNullOrEmpty(article))
                return Json(HttpStatusCode.BadRequest, new { status = false, message = "store and article are required" });

            long artNum;
            if (!long.TryParse(article, out artNum))
                return Json(HttpStatusCode.BadRequest, new { status = false, message = "article must be numeric (variant article number)" });

            try
            {
                var (typeVal, sizeRaw, _, loadMs) = ArticleLookupCache.Get(store, artNum);
                string sizeVal = sizeRaw == "BS" ? "BS" : sizeRaw == "S" ? "S" : "NORMAL";
                return Json(HttpStatusCode.OK, new {
                    status        = true,
                    store         = store,
                    article       = article,
                    article_type  = typeVal,
                    article_size  = sizeVal,
                    source        = "cache",
                    cache_load_ms = loadMs,
                    ms            = (int)(DateTime.UtcNow - t0).TotalMilliseconds
                });
            }
            catch (Exception ex)
            {
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

        /// <summary>Flush cache. Pass {store} to flush one store, or empty body to flush all.</summary>
        [HttpPost, Route("api/article-lookup/invalidate")]
        [ResponseType(typeof(ArticleLookupInvalidateResponse))]
        public HttpResponseMessage Invalidate([FromBody] ArticleLookupInvalidateRequest req)
        {
            string store = req?.Store;
            if (string.IsNullOrEmpty(store)) ArticleLookupCache.InvalidateAll();
            else ArticleLookupCache.InvalidateStore(store);
            return Json(HttpStatusCode.OK, new { invalidated = store ?? "(all)", status = ArticleLookupCache.Status() });
        }
    }
}
