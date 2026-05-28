using SAP.Middleware.Connector;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.MM
{
    /// <summary>
    /// PO Read API — direct SAP→Lovable/Snowflake bypass for purchase-order data.
    ///
    /// RFC:     ZMM_PO_DETAILS (FMODE='R' in TFDIR)
    /// Source:  EKKO+EKPO, returns PO header summary (1 row per purchasing doc)
    /// Output:  IT_FINAL (ZPO_RFC_TT) — 8 fields:
    ///          PURCHASING_DOC, PO_TYPE, CREATED_ON, CREATED_BY,
    ///          SUPPLIER, NET_VALUE, PO_QUANITY, PLANT
    ///
    /// Endpoints:
    ///   GET  /api/po?DateFrom=2026-05-26&Plant=DH24&Limit=500   (data-lake style)
    ///   POST /api/po  body: { "DateFrom":"2026-05-26", ... }    (Lovable/script style)
    /// Auth:     X-RFC-Key: v2-rfc-proxy-2026
    ///
    /// Params (query string OR JSON body, same fields):
    ///   DateFrom  YYYY-MM-DD (required)
    ///   DateTo    YYYY-MM-DD (optional, defaults to DateFrom)
    ///   Plant     optional client-side filter
    ///   Vendor    optional client-side filter
    ///   Offset    pagination offset (default 0)
    ///   Limit     page size (default 1000, max 50000)
    ///   env       dev | qa | prod (default prod) — query string only
    ///
    /// Safeguards:
    /// - DateFrom required
    /// - Max window 31 days
    /// - Pagination via Offset + Limit
    /// - 5min in-memory cache keyed (env, dates, plant, vendor) so paginated
    ///   follow-up calls don't re-hit SAP
    /// </summary>
    public class PoReadController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";
        private const int MAX_WINDOW_DAYS = 31;
        private const int DEFAULT_LIMIT = 100;
        private const int MAX_LIMIT = 50000;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new ConcurrentDictionary<string, CacheEntry>();

        public class PoReadRequest
        {
            public string DateFrom { get; set; }
            public string DateTo { get; set; }
            public string Plant { get; set; }
            public string Vendor { get; set; }
            public int? Offset { get; set; }
            public int? Limit { get; set; }
        }

        [HttpGet, Route("api/po")]
        public IHttpActionResult Get([FromUri] PoReadRequest req, string env = "prod")
        {
            return Execute(req, env);
        }

        [HttpPost, Route("api/po")]
        public IHttpActionResult Post([FromBody] PoReadRequest req, string env = "prod")
        {
            return Execute(req, env);
        }

        private IHttpActionResult Execute(PoReadRequest req, string env)
        {
            if (!IsAuthorized())
                return Json(new { Success = false, Error = "Unauthorized — missing or invalid X-RFC-Key" });

            if (req == null) req = new PoReadRequest();
            if (string.IsNullOrWhiteSpace(req.DateFrom))
                return Json(new { Success = false, Error = "DateFrom (YYYY-MM-DD) is required." });

            string dateTo = string.IsNullOrWhiteSpace(req.DateTo) ? req.DateFrom : req.DateTo;

            DateTime fromDt, toDt;
            try
            {
                fromDt = ParseIsoDate(req.DateFrom, "DateFrom");
                toDt = ParseIsoDate(dateTo, "DateTo");
            }
            catch (FormatException fx)
            {
                return Json(new { Success = false, Error = fx.Message });
            }

            if (toDt < fromDt)
                return Json(new { Success = false, Error = "DateTo must be >= DateFrom." });

            int windowDays = (toDt - fromDt).Days + 1;
            if (windowDays > MAX_WINDOW_DAYS)
                return Json(new
                {
                    Success = false,
                    Error = "Date window " + windowDays + " days exceeds cap of " + MAX_WINDOW_DAYS + " days. Split into multiple calls."
                });

            string envNorm = (env ?? "prod").Trim().ToLowerInvariant();
            RfcConfigParameters rfcPar;
            string envLabel;
            switch (envNorm)
            {
                case "dev":
                case "development":
                    rfcPar = BaseController.rfcConfigparameters();
                    envLabel = "dev";
                    break;
                case "qa":
                case "quality":
                    rfcPar = BaseController.rfcConfigparametersquality();
                    envLabel = "qa";
                    break;
                case "prod":
                case "production":
                case "":
                    rfcPar = BaseController.rfcConfigparametersproduction();
                    envLabel = "prod";
                    break;
                default:
                    return Json(new { Success = false, Error = "Invalid env '" + env + "'. Use dev | qa | prod." });
            }

            int offset = Math.Max(0, req.Offset.GetValueOrDefault(0));
            int limit = req.Limit.GetValueOrDefault(DEFAULT_LIMIT);
            if (limit <= 0) limit = DEFAULT_LIMIT;
            if (limit > MAX_LIMIT) limit = MAX_LIMIT;

            string plantFilter = TrimOrNull(req.Plant);
            string vendorFilter = TrimOrNull(req.Vendor);
            string cacheKey = BuildCacheKey(envLabel, req.DateFrom, dateTo, plantFilter, vendorFilter);

            CacheEntry entry;
            bool fromCache = false;
            if (Cache.TryGetValue(cacheKey, out entry) && entry.ExpiresAt > DateTime.UtcNow)
            {
                fromCache = true;
            }
            else
            {
                try
                {
                    entry = FetchFromSap(rfcPar, ToSapDate(req.DateFrom), ToSapDate(dateTo), plantFilter, vendorFilter);
                    entry.ExpiresAt = DateTime.UtcNow.Add(CacheTtl);
                    Cache[cacheKey] = entry;
                    PruneExpired();
                }
                catch (RfcAbapException ex)
                {
                    return Json(new { Success = false, Error = "SAP ABAP error: " + ex.Message });
                }
                catch (RfcCommunicationException ex)
                {
                    return Json(new { Success = false, Error = "SAP connection error: " + ex.Message });
                }
                catch (RfcLogonException ex)
                {
                    return Json(new { Success = false, Error = "SAP logon error: " + ex.Message });
                }
                catch (Exception ex)
                {
                    return Json(new { Success = false, Error = "Error: " + ex.Message });
                }
            }

            int total = entry.Rows.Count;
            var page = new List<object>(Math.Min(limit, Math.Max(0, total - offset)));
            for (int i = offset; i < total && page.Count < limit; i++)
                page.Add(entry.Rows[i]);

            bool hasMore = offset + page.Count < total;
            bool isFailure = total == 0 && !string.IsNullOrEmpty(entry.SapMessage) &&
                             entry.SapMessage.Equals("No Data Found", StringComparison.OrdinalIgnoreCase);

            return Json(new
            {
                Success = !isFailure,
                Source = "ZMM_PO_DETAILS",
                Env = envLabel,
                DateFrom = req.DateFrom,
                DateTo = dateTo,
                Offset = offset,
                Limit = limit,
                TotalRows = total,
                RowCount = page.Count,
                HasMore = hasMore,
                NextOffset = hasMore ? (int?)(offset + page.Count) : null,
                FromCache = fromCache,
                SapMessage = string.IsNullOrEmpty(entry.SapMessage) ? null : entry.SapMessage,
                Rows = page
            });
        }

        private class CacheEntry
        {
            public List<object> Rows;
            public string SapMessage;
            public DateTime ExpiresAt;
        }

        private static CacheEntry FetchFromSap(
            RfcConfigParameters rfcPar, string sapFrom, string sapTo, string plantFilter, string vendorFilter)
        {
            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction fn = dest.Repository.CreateFunction("ZMM_PO_DETAILS");
            fn.SetValue("IT_CREATED_LOW", sapFrom);
            fn.SetValue("IT_CREATED_HIGH", sapTo);
            fn.Invoke(dest);

            IRfcTable rows = fn.GetTable("IT_FINAL");
            string vendorMatch = string.IsNullOrEmpty(vendorFilter) ? null : vendorFilter.TrimStart('0');

            var output = new List<object>(rows.RowCount);
            foreach (IRfcStructure row in rows)
            {
                string plant = SafeGet(row, "PLANT");
                if (plantFilter != null && !string.Equals(plant, plantFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                string supplier = SafeGet(row, "SUPPLIER");
                if (vendorMatch != null && !string.Equals((supplier ?? "").TrimStart('0'), vendorMatch, StringComparison.OrdinalIgnoreCase))
                    continue;

                output.Add(new
                {
                    PurchasingDoc = SafeGet(row, "PURCHASING_DOC"),
                    PoType = SafeGet(row, "PO_TYPE"),
                    CreatedOn = SafeGet(row, "CREATED_ON"),
                    CreatedBy = SafeGet(row, "CREATED_BY"),
                    Supplier = supplier,
                    NetValue = SafeGet(row, "NET_VALUE"),
                    PoQuantity = SafeGet(row, "PO_QUANITY"),
                    Plant = plant
                });
            }

            IRfcStructure ret = fn.GetStructure("EX_RETURN");
            return new CacheEntry { Rows = output, SapMessage = ret.GetString("MESSAGE") ?? "" };
        }

        private static void PruneExpired()
        {
            if (Cache.Count < 64) return;
            DateTime now = DateTime.UtcNow;
            foreach (var kv in Cache)
            {
                if (kv.Value.ExpiresAt <= now)
                {
                    CacheEntry ignored;
                    Cache.TryRemove(kv.Key, out ignored);
                }
            }
        }

        private bool IsAuthorized()
        {
            IEnumerable<string> headers;
            if (!Request.Headers.TryGetValues("X-RFC-Key", out headers)) return false;
            foreach (var h in headers)
                if (string.Equals(h, API_KEY, StringComparison.Ordinal)) return true;
            return false;
        }

        private static DateTime ParseIsoDate(string s, string fieldName)
        {
            DateTime dt;
            if (!DateTime.TryParseExact((s ?? "").Trim(), new[] { "yyyy-MM-dd", "yyyyMMdd" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                throw new FormatException(fieldName + " '" + s + "' must be YYYY-MM-DD.");
            }
            return dt;
        }

        private static string ToSapDate(string isoDate)
        {
            return ParseIsoDate(isoDate, "date").ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string TrimOrNull(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static string BuildCacheKey(string env, string from, string to, string plant, string vendor)
        {
            string raw = "po|" + env + "|" + from + "|" + to + "|" + (plant ?? "") + "|" + (vendor ?? "");
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return Convert.ToBase64String(hash);
            }
        }

        private static string SafeGet(IRfcStructure row, string field)
        {
            try { return row.GetString(field); } catch { return null; }
        }
    }
}
