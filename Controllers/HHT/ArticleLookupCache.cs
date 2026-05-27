using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Vendor_Application_MVC.Controllers.HHT
{
    // In-process snapshot of dbo.L_VAR_ARTICLE + dbo.SZ_GROUP_VAR_ARTICLE.
    // Server 28 has no index on (STORE, ARTICLE_NUMBER) for these heaps; live SQL
    // takes 5-15 s per cold lookup. We mirror the columns we need into memory
    // (~150 MB) so /api/article-lookup serves <10 ms.
    //
    // Refresh every 60 minutes via Timer. Atomic swap of dict references.
    // First-call cold path: returns null + spawns load; controller falls back to live SQL.
    internal static class ArticleLookupCache
    {
        // listing[store_upper][article] -> 1=L, 2=RL, 0=NL
        private static volatile Dictionary<string, Dictionary<long, byte>> _listing
            = new Dictionary<string, Dictionary<long, byte>>(StringComparer.OrdinalIgnoreCase);
        // sizes[article] -> 1=BS, 2=S, 0=N(NORMAL)
        private static volatile Dictionary<long, byte> _sizes
            = new Dictionary<long, byte>();
        // Active discounts effective today. discount type wins over listing.
        private static volatile HashSet<long> _discountAll
            = new HashSet<long>();
        private static volatile Dictionary<string, HashSet<long>> _discountByStore
            = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
        private static int _discountRows = 0;

        private static DateTime _loadedAtUtc = DateTime.MinValue;
        private static DateTime _refreshStartedUtc = DateTime.MinValue;
        private static int _listingRows = 0;
        private static int _sizeRows = 0;
        private static long _lastLoadMs = 0;
        private static string _lastError = null;
        private static int _loading = 0; // 0=idle, 1=running
        private static Timer _timer;

        private const int REFRESH_MINUTES = 60;

        public static bool IsLoaded => _loadedAtUtc != DateTime.MinValue;
        public static DateTime LoadedAtUtc => _loadedAtUtc;
        public static int ListingRows => _listingRows;
        public static int SizeRows => _sizeRows;
        public static long LastLoadMs => _lastLoadMs;
        public static string LastError => _lastError;
        public static bool Loading => Volatile.Read(ref _loading) == 1;

        public static (string typeVal, string sizeRaw)? TryGet(string store, long article)
        {
            if (!IsLoaded) { EnsureLoadStarted(); return null; }
            var listing = _listing;
            var sizes = _sizes;
            var dAll = _discountAll;
            var dByStore = _discountByStore;

            string typeVal;
            bool isDisc = dAll.Contains(article)
                || (dByStore.TryGetValue(store, out var dset) && dset.Contains(article));
            if (isDisc) typeVal = "C";
            else if (listing.TryGetValue(store, out var artDict) && artDict.TryGetValue(article, out var b))
                typeVal = b == 1 ? "L" : b == 2 ? "RL" : "NL";
            else typeVal = "NL";

            string sizeRaw = sizes.TryGetValue(article, out var sb) ? (sb == 1 ? "BS" : sb == 2 ? "S" : "N") : "N";
            return (typeVal, sizeRaw);
        }

        public static void EnsureLoadStarted()
        {
            if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0) return;
            _refreshStartedUtc = DateTime.UtcNow;
            Task.Run(() => {
                try { LoadOnce(); }
                catch (Exception ex) { _lastError = ex.Message; }
                finally { Volatile.Write(ref _loading, 0); }
            });
            EnsureTimer();
        }

        private static void EnsureTimer()
        {
            if (_timer != null) return;
            lock (typeof(ArticleLookupCache))
            {
                if (_timer != null) return;
                _timer = new Timer(_ => {
                    if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0) return;
                    _refreshStartedUtc = DateTime.UtcNow;
                    try { LoadOnce(); }
                    catch (Exception ex) { _lastError = ex.Message; }
                    finally { Volatile.Write(ref _loading, 0); }
                }, null, TimeSpan.FromMinutes(REFRESH_MINUTES), TimeSpan.FromMinutes(REFRESH_MINUTES));
            }
        }

        private static void LoadOnce()
        {
            var sw = Stopwatch.StartNew();

            // Build new dictionaries, then atomically swap.
            var newListing = new Dictionary<string, Dictionary<long, byte>>(StringComparer.OrdinalIgnoreCase);
            var newSizes = new Dictionary<long, byte>(1_500_000);
            var newDiscountAll = new HashSet<long>();
            var newDiscountByStore = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
            int lRows = 0, sRows = 0, dRows = 0;

            using (var conn = GetConnection())
            {
                using (var cmd = new SqlCommand(
                    "SELECT STORE, ARTICLE_NUMBER, L_STATUS FROM dbo.L_VAR_ARTICLE WITH(NOLOCK)", conn)
                { CommandTimeout = 600 })
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd.IsDBNull(0) || rd.IsDBNull(1)) continue;
                        string st = rd.GetString(0);
                        long art = rd.GetInt64(1);
                        string status = rd.IsDBNull(2) ? "NL" : rd.GetString(2);
                        byte sb = status == "L" ? (byte)1 : status == "RL" ? (byte)2 : (byte)0;
                        if (!newListing.TryGetValue(st, out var inner))
                        {
                            inner = new Dictionary<long, byte>(16);
                            newListing[st] = inner;
                        }
                        inner[art] = sb;
                        lRows++;
                    }
                }

                using (var cmd = new SqlCommand(
                    "SELECT ARTICLE_NUMBER, SIZE_GRP FROM dbo.SZ_GROUP_VAR_ARTICLE WITH(NOLOCK)", conn)
                { CommandTimeout = 600 })
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd.IsDBNull(0)) continue;
                        long art = rd.GetInt64(0);
                        string sz = rd.IsDBNull(1) ? "N" : rd.GetString(1);
                        byte sb = sz == "BS" ? (byte)1 : sz == "S" ? (byte)2 : (byte)0;
                        newSizes[art] = sb;
                        sRows++;
                    }
                }

                using (var cmd = new SqlCommand(
                    @"SELECT ST_CD, MATNR FROM dbo.ST_ART_DISCOUNT WITH(NOLOCK)
                       WHERE CAST(GETDATE() AS DATE) BETWEEN VALID_FROM_DT AND VALID_TO_DT", conn)
                { CommandTimeout = 60 })
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (rd.IsDBNull(1)) continue;
                        string st = rd.IsDBNull(0) ? "" : rd.GetString(0);
                        long art = rd.GetInt64(1);
                        if (string.IsNullOrEmpty(st) || string.Equals(st, "All", StringComparison.OrdinalIgnoreCase))
                            newDiscountAll.Add(art);
                        else
                        {
                            if (!newDiscountByStore.TryGetValue(st, out var set))
                            {
                                set = new HashSet<long>();
                                newDiscountByStore[st] = set;
                            }
                            set.Add(art);
                        }
                        dRows++;
                    }
                }
            }

            _listing = newListing;
            _sizes = newSizes;
            _discountAll = newDiscountAll;
            _discountByStore = newDiscountByStore;
            _listingRows = lRows;
            _sizeRows = sRows;
            _discountRows = dRows;
            _loadedAtUtc = DateTime.UtcNow;
            _lastLoadMs = sw.ElapsedMilliseconds;
            _lastError = null;
        }

        // Mirror of ArticleLookupController.GetConnection to keep the cache self-contained.
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

        public static object Status() => new {
            loaded             = IsLoaded,
            loading            = Loading,
            loaded_at_utc      = _loadedAtUtc == DateTime.MinValue ? null : (object)_loadedAtUtc.ToString("o"),
            refresh_started_utc= _refreshStartedUtc == DateTime.MinValue ? null : (object)_refreshStartedUtc.ToString("o"),
            listing_rows       = _listingRows,
            size_rows          = _sizeRows,
            discount_rows      = _discountRows,
            last_load_ms       = _lastLoadMs,
            refresh_minutes    = REFRESH_MINUTES,
            last_error         = _lastError
        };
    }
}
