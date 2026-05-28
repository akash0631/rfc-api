using SAP.Middleware.Connector;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    /// <summary>
    /// GRN/GRC Read API — direct SAP→Lovable/Snowflake bypass for goods-receipt data.
    ///
    /// RFC:     ZFI_GRC_DETAILS_RFC (FMODE='R', server-side date filter on CPUDT)
    /// Output:  IT_DATA (ZFI_GRC_DETAILS_TT) — 17 fields incl. PURCHASE_ORDER
    ///
    /// Endpoints:
    ///   GET  /api/grn?DateFrom=2026-05-26&Plant=DH24&Limit=500
    ///   POST /api/grn  body: { "DateFrom":"2026-05-26", ... }
    /// Auth:     X-RFC-Key: v2-rfc-proxy-2026
    ///
    /// Params (query string OR JSON body):
    ///   DateFrom       YYYY-MM-DD (required, SAP-side filter on CPUDT)
    ///   DateTo         YYYY-MM-DD (optional, defaults to DateFrom)
    ///   Plant          optional client-side filter (WERKS)
    ///   Vendor         optional client-side filter (LIFNR, zero-strip)
    ///   MovementType   optional BWART filter (101=GR, 102=GR-reverse)
    ///   PurchaseOrder  optional EBELN filter (zero-strip)
    ///   Offset         pagination offset (default 0)
    ///   Limit          page size (default 1000, max 50000)
    ///   env            dev | qa | prod (default prod) — query string only
    ///
    /// Safeguards:
    /// - DateFrom required
    /// - Max window 7 days (GR volume ~5-10K rows/day across all plants)
    /// - 5min in-memory cache keyed (env, dates, all filters)
    /// </summary>
    public class GrnReadController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";
        private const int MAX_WINDOW_DAYS = 7;
        private const int DEFAULT_LIMIT = 100;
        private const int MAX_LIMIT = 50000;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new ConcurrentDictionary<string, CacheEntry>();

        public class GrnReadRequest
        {
            public string DateFrom { get; set; }
            public string DateTo { get; set; }
            public string Plant { get; set; }
            public string Vendor { get; set; }
            public string MovementType { get; set; }
            public string PurchaseOrder { get; set; }
            public int? Offset { get; set; }
            public int? Limit { get; set; }
        }

        [HttpGet, Route("api/grn")]
        public IHttpActionResult Get([FromUri] GrnReadRequest req, string env = "prod")
        {
            return Execute(req, env);
        }

        [HttpPost, Route("api/grn")]
        public IHttpActionResult Post([FromBody] GrnReadRequest req, string env = "prod")
        {
            return Execute(req, env);
        }

        private IHttpActionResult Execute(GrnReadRequest req, string env)
        {
            if (!IsAuthorized())
                return Json(new { Success = false, Error = "Unauthorized — missing or invalid X-RFC-Key" });

            if (req == null) req = new GrnReadRequest();
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
            string mvtFilter = TrimOrNull(req.MovementType);
            string poFilter = TrimOrNull(req.PurchaseOrder);
            string cacheKey = BuildCacheKey(envLabel, req.DateFrom, dateTo, plantFilter, vendorFilter, mvtFilter, poFilter);

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
                    entry = FetchFromSap(rfcPar, ToSapDate(req.DateFrom), ToSapDate(dateTo),
                                         plantFilter, vendorFilter, mvtFilter, poFilter);
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

            return Json(new
            {
                Success = true,
                Source = "ZFI_GRC_DETAILS_RFC",
                Env = envLabel,
                DateFrom = req.DateFrom,
                DateTo = dateTo,
                Plant = plantFilter,
                Vendor = req.Vendor,
                MovementType = mvtFilter,
                PurchaseOrder = poFilter,
                Offset = offset,
                Limit = limit,
                TotalRows = total,
                RowCount = page.Count,
                HasMore = hasMore,
                NextOffset = hasMore ? (int?)(offset + page.Count) : null,
                FromCache = fromCache,
                Rows = page
            });
        }

        private class CacheEntry
        {
            public List<object> Rows;
            public DateTime ExpiresAt;
        }

        private static CacheEntry FetchFromSap(
            RfcConfigParameters rfcPar, string sapFrom, string sapTo,
            string plantFilter, string vendorFilter, string mvtFilter, string poFilter)
        {
            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction fn = dest.Repository.CreateFunction("ZFI_GRC_DETAILS_RFC");
            fn.SetValue("IM_ENTERED_LOW", sapFrom);
            fn.SetValue("IM_ENTERED_HIGH", sapTo);
            fn.Invoke(dest);

            IRfcTable rows = fn.GetTable("IT_DATA");
            string vendorMatch = string.IsNullOrEmpty(vendorFilter) ? null : vendorFilter.TrimStart('0');
            string poMatch = string.IsNullOrEmpty(poFilter) ? null : poFilter.TrimStart('0');

            var output = new List<object>(rows.RowCount);
            foreach (IRfcStructure row in rows)
            {
                string plant = SafeGet(row, "PLANT");
                if (plantFilter != null && !string.Equals(plant, plantFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                string supplier = SafeGet(row, "SUPPLIER");
                if (vendorMatch != null && !string.Equals((supplier ?? "").TrimStart('0'), vendorMatch, StringComparison.OrdinalIgnoreCase))
                    continue;

                string bwart = SafeGet(row, "MOVEMENT_TYPE");
                if (mvtFilter != null && !string.Equals(bwart, mvtFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                string po = SafeGet(row, "PURCHASE_ORDER");
                if (poMatch != null && !string.Equals((po ?? "").TrimStart('0'), poMatch, StringComparison.OrdinalIgnoreCase))
                    continue;

                output.Add(new
                {
                    MaterialDoc = SafeGet(row, "MATERIAL_DOC"),
                    Year = SafeGet(row, "MAT_DOC_YEAR"),
                    MovementType = bwart,
                    Plant = plant,
                    Supplier = supplier,
                    DebitCredit = SafeGet(row, "DEBIT_CREDIT"),
                    AmountInLC = SafeGet(row, "AMOUNT_IN_LC"),
                    Quantity = SafeGet(row, "QUANTITY"),
                    BaseUnit = SafeGet(row, "BASE_UNIT"),
                    PurchaseOrder = po,
                    ReferenceDoc = SafeGet(row, "REFERENCE_DOC"),
                    SupplierReceive = SafeGet(row, "SUPPLIER_RECEIVE"),
                    TransEvType = SafeGet(row, "TRANS_EV_TYPE"),
                    PostingDate = SafeGet(row, "POSTING_DATE"),
                    EnteredOn = SafeGet(row, "ENTERED_ON"),
                    Text = SafeGet(row, "TEXT"),
                    MovementWM = SafeGet(row, "MOVEMENT_WM")
                });
            }

            return new CacheEntry { Rows = output };
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

        private static string BuildCacheKey(string env, string from, string to,
            string plant, string vendor, string mvt, string po)
        {
            string raw = "grn|" + env + "|" + from + "|" + to + "|" + (plant ?? "") + "|" +
                         (vendor ?? "") + "|" + (mvt ?? "") + "|" + (po ?? "");
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
