using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace Vendor_Application_MVC.Controllers.HHT
{
    // Per-store, in-request lazy cache for /api/article-lookup.
    //
    // Why per-store: an HHT operator is always inside ONE store at a time, so we
    // only need that store's articles in memory (~12 k rows = ~1 MB per store).
    // Full-table caching (3.9 M rows / ~150 MB) exceeded V2RfcTestPool's 1 GB
    // private-memory cap during the initial Dictionary build and the pool was
    // recycled mid-load.
    //
    // Why in-request: long-lived background threads inside IIS get killed on
    // ASP.NET shutdown / recycle. A per-request synchronous load lives inside
    // the active HTTP request and is allowed to run to completion (110 s default
    // request timeout).
    //
    // Why store-scoped at the DB layer: dbo.L_VAR_ARTICLE has no covering index,
    // so the first scan in a fresh buffer pool pays full heap-scan cost (~5–15 s).
    // After the first query the pages stay hot in Server 28's buffer pool and
    // subsequent per-store scans drop to ~1–3 s. App-side cache then makes every
    // call inside that store <10 ms until the 60-min TTL expires.
    internal static class ArticleLookupCache
    {
        private sealed class StoreSnapshot
        {
            public Dictionary<long, (byte status, byte size)> Articles;
            public DateTime LoadedUtc;
            public long LoadMs;
        }

        private static readonly Dictionary<string, StoreSnapshot> _stores
            = new Dictionary<string, StoreSnapshot>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, object> _storeLocks
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _globalLock = new object();

        // Discount cache (small table, ~tens of rows). Refreshed when older than TTL.
        private static volatile HashSet<long> _discountAll = new HashSet<long>();
        private static volatile Dictionary<string, HashSet<long>> _discountByStore
            = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _discountLoadedUtc = DateTime.MinValue;
        private static int _discountRows = 0;
        private static readonly object _discountLock = new object();

        private static readonly DateTime _processStartUtc = DateTime.UtcNow;
        private static int _hits = 0, _misses = 0;

        private const int STORE_TTL_MIN = 60;
        private const int DISCOUNT_TTL_MIN = 5;

        public static (string typeVal, string sizeRaw, bool fromCache, long loadMs) Get(string store, long article)
        {
            EnsureDiscountFresh();
            var snap = EnsureStoreFresh(store);

            bool isDisc = _discountAll.Contains(article)
                || (_discountByStore.TryGetValue(store, out var dset) && dset.Contains(article));

            string typeVal;
            string sizeRaw;
            if (snap.Articles.TryGetValue(article, out var entry))
            {
                typeVal = isDisc ? "C" : (entry.status == 1 ? "L" : entry.status == 2 ? "RL" : "NL");
                sizeRaw = entry.size == 1 ? "BS" : entry.size == 2 ? "S" : "N";
            }
            else
            {
                typeVal = isDisc ? "C" : "NL";
                sizeRaw = "N";
            }
            Interlocked.Increment(ref _hits);
            return (typeVal, sizeRaw, true, snap.LoadMs);
        }

        private static StoreSnapshot EnsureStoreFresh(string store)
        {
            StoreSnapshot snap;
            lock (_globalLock)
            {
                _stores.TryGetValue(store, out snap);
                if (snap != null && (DateTime.UtcNow - snap.LoadedUtc).TotalMinutes < STORE_TTL_MIN)
                    return snap;
            }

            object slock;
            lock (_globalLock)
            {
                if (!_storeLocks.TryGetValue(store, out slock))
                    _storeLocks[store] = slock = new object();
            }

            lock (slock)
            {
                lock (_globalLock) { _stores.TryGetValue(store, out snap); }
                if (snap != null && (DateTime.UtcNow - snap.LoadedUtc).TotalMinutes < STORE_TTL_MIN)
                    return snap;

                Interlocked.Increment(ref _misses);
                snap = LoadStore(store);
                lock (_globalLock) { _stores[store] = snap; }
                return snap;
            }
        }

        private static StoreSnapshot LoadStore(string store)
        {
            var sw = Stopwatch.StartNew();
            var dict = new Dictionary<long, (byte, byte)>(20_000);

            const string sql = @"
SELECT l.ARTICLE_NUMBER,
       l.L_STATUS,
       s.SIZE_GRP
  FROM dbo.L_VAR_ARTICLE l WITH(NOLOCK)
  LEFT JOIN dbo.SZ_GROUP_VAR_ARTICLE s WITH(NOLOCK)
    ON s.ARTICLE_NUMBER = l.ARTICLE_NUMBER
 WHERE l.STORE = @store;";

            using (var conn = GetConnection())
            using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 90 })
            {
                cmd.Parameters.Add(new SqlParameter("@store", SqlDbType.NVarChar, 50) { Value = store });
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd.IsDBNull(0)) continue;
                        long art = rd.GetInt64(0);
                        string status = rd.IsDBNull(1) ? "NL" : rd.GetString(1);
                        string size = rd.IsDBNull(2) ? "N" : rd.GetString(2);
                        byte sb = status == "L" ? (byte)1 : status == "RL" ? (byte)2 : (byte)0;
                        byte zb = size == "BS" ? (byte)1 : size == "S" ? (byte)2 : (byte)0;
                        dict[art] = (sb, zb);
                    }
                }
            }

            return new StoreSnapshot {
                Articles = dict,
                LoadedUtc = DateTime.UtcNow,
                LoadMs = sw.ElapsedMilliseconds
            };
        }

        private static void EnsureDiscountFresh()
        {
            if (_discountLoadedUtc != DateTime.MinValue
                && (DateTime.UtcNow - _discountLoadedUtc).TotalMinutes < DISCOUNT_TTL_MIN) return;

            lock (_discountLock)
            {
                if (_discountLoadedUtc != DateTime.MinValue
                    && (DateTime.UtcNow - _discountLoadedUtc).TotalMinutes < DISCOUNT_TTL_MIN) return;

                var newAll = new HashSet<long>();
                var newByStore = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
                int rows = 0;

                using (var conn = GetConnection())
                using (var cmd = new SqlCommand(
                    @"SELECT ST_CD, MATNR FROM dbo.ST_ART_DISCOUNT WITH(NOLOCK)
                       WHERE CAST(GETDATE() AS DATE) BETWEEN VALID_FROM_DT AND VALID_TO_DT", conn)
                    { CommandTimeout = 30 })
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd.IsDBNull(1)) continue;
                        string st = rd.IsDBNull(0) ? "" : rd.GetString(0);
                        long art = rd.GetInt64(1);
                        if (string.IsNullOrEmpty(st) || string.Equals(st, "All", StringComparison.OrdinalIgnoreCase))
                            newAll.Add(art);
                        else
                        {
                            if (!newByStore.TryGetValue(st, out var set))
                            {
                                set = new HashSet<long>();
                                newByStore[st] = set;
                            }
                            set.Add(art);
                        }
                        rows++;
                    }
                }
                _discountAll = newAll;
                _discountByStore = newByStore;
                _discountRows = rows;
                _discountLoadedUtc = DateTime.UtcNow;
            }
        }

        public static object Status()
        {
            lock (_globalLock)
            {
                var stores = new List<object>();
                foreach (var kv in _stores)
                    stores.Add(new {
                        store = kv.Key,
                        articles = kv.Value.Articles.Count,
                        loaded_utc = kv.Value.LoadedUtc.ToString("o"),
                        load_ms = kv.Value.LoadMs,
                        age_min = (int)(DateTime.UtcNow - kv.Value.LoadedUtc).TotalMinutes
                    });
                return new {
                    pid = Process.GetCurrentProcess().Id,
                    appdomain_id = AppDomain.CurrentDomain.Id,
                    process_age_sec = (int)(DateTime.UtcNow - _processStartUtc).TotalSeconds,
                    store_count = _stores.Count,
                    discount_rows = _discountRows,
                    discount_loaded_utc = _discountLoadedUtc == DateTime.MinValue ? null : (object)_discountLoadedUtc.ToString("o"),
                    discount_age_sec = _discountLoadedUtc == DateTime.MinValue ? -1 : (int)(DateTime.UtcNow - _discountLoadedUtc).TotalSeconds,
                    store_ttl_min = STORE_TTL_MIN,
                    discount_ttl_min = DISCOUNT_TTL_MIN,
                    hits = _hits,
                    misses = _misses,
                    stores
                };
            }
        }

        public static void InvalidateStore(string store)
        {
            lock (_globalLock) { _stores.Remove(store ?? ""); }
        }

        public static void InvalidateAll()
        {
            lock (_globalLock) { _stores.Clear(); }
            _discountLoadedUtc = DateTime.MinValue;
        }

        // Connection helpers (mirror ArticleLookupController.GetConnection so the cache is self-contained).
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain,
            string lpszPassword, int dwLogonType, int dwLogonProvider, out IntPtr phToken);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);
        private const string WIN_DOMAIN = "V2RD";
        private const string WIN_USER = "akash.agarwal";
        private const string WIN_PASS = "vrl@99999";
        private static readonly string CS_INTEGRATED =
            @"Server=192.168.151.28;Database=DataV2;Integrated Security=True;Connection Timeout=10;MultipleActiveResultSets=true;";
        private static readonly string CS_FALLBACK =
            @"Server=192.168.151.28;Database=DataV2;User Id=V2ApiReader;Password=V2Api@2026;Connection Timeout=10;MultipleActiveResultSets=true;";

        private static SqlConnection GetConnection()
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (LogonUser(WIN_USER, WIN_DOMAIN, WIN_PASS, 9, 0, out token) && token != IntPtr.Zero)
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
    }
}
