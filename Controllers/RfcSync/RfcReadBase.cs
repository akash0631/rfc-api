using SAP.Middleware.Connector;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Base for read-only RFC wrapper controllers. Provides:
    ///   - X-RFC-Key auth
    ///   - dev|qa|prod env switch
    ///   - date validation + window cap
    ///   - Offset/Limit pagination
    ///   - 5min in-memory result cache (ConcurrentDictionary, expiring entries)
    ///   - standard JSON envelope: { Success, Source, Env, Offset, Limit,
    ///                                TotalRows, RowCount, HasMore, NextOffset,
    ///                                FromCache, Rows }
    ///
    /// Subclasses implement FetchFromSap() — call SAP RFC, return list of row objects.
    /// </summary>
    public abstract class RfcReadBase : BaseController
    {
        protected const string API_KEY = "v2-rfc-proxy-2026";
        protected const int DEFAULT_LIMIT = 100;
        protected const int MAX_LIMIT = 50000;
        protected static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        protected class CacheEntry
        {
            public List<object> Rows;
            public string SapMessage;
            public DateTime ExpiresAt;
        }

        public class ReadRequest
        {
            public string DateFrom { get; set; }
            public string DateTo { get; set; }
            public string Plant { get; set; }
            public string Vendor { get; set; }
            public string Article { get; set; }
            public int? Offset { get; set; }
            public int? Limit { get; set; }
        }

        protected IHttpActionResult RunRead(
            ReadRequest req,
            string env,
            string sourceLabel,
            int maxWindowDays,
            bool requireDate,
            ConcurrentDictionary<string, CacheEntry> cache,
            Func<RfcConfigParameters, ReadRequest, string, string, CacheEntry> fetcher)
        {
            if (!IsAuthorized())
                return Json(new { Success = false, Error = "Unauthorized — missing or invalid X-RFC-Key" });

            if (req == null) req = new ReadRequest();

            string sapFrom = null, sapTo = null;
            if (requireDate)
            {
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
                if (windowDays > maxWindowDays)
                    return Json(new
                    {
                        Success = false,
                        Error = "Date window " + windowDays + " days exceeds cap of " + maxWindowDays + " days. Split into multiple calls."
                    });

                req.DateTo = dateTo;
                sapFrom = fromDt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                sapTo = toDt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }

            RfcConfigParameters rfcPar;
            string envLabel;
            string envErr = ResolveEnv(env, out rfcPar, out envLabel);
            if (envErr != null) return Json(new { Success = false, Error = envErr });

            int offset = Math.Max(0, req.Offset.GetValueOrDefault(0));
            int limit = req.Limit.GetValueOrDefault(DEFAULT_LIMIT);
            if (limit <= 0) limit = DEFAULT_LIMIT;
            if (limit > MAX_LIMIT) limit = MAX_LIMIT;

            string cacheKey = BuildCacheKey(sourceLabel, envLabel, req);
            CacheEntry entry;
            bool fromCache = false;
            if (cache.TryGetValue(cacheKey, out entry) && entry.ExpiresAt > DateTime.UtcNow)
            {
                fromCache = true;
            }
            else
            {
                try
                {
                    entry = fetcher(rfcPar, req, sapFrom, sapTo);
                    entry.ExpiresAt = DateTime.UtcNow.Add(CacheTtl);
                    cache[cacheKey] = entry;
                    PruneExpired(cache);
                }
                catch (RfcAbapException ex) { return Json(new { Success = false, Error = "SAP ABAP error: " + ex.Message }); }
                catch (RfcCommunicationException ex) { return Json(new { Success = false, Error = "SAP connection error: " + ex.Message }); }
                catch (RfcLogonException ex) { return Json(new { Success = false, Error = "SAP logon error: " + ex.Message }); }
                catch (Exception ex) { return Json(new { Success = false, Error = "Error: " + ex.Message }); }
            }

            int total = entry.Rows.Count;
            var page = new List<object>(Math.Min(limit, Math.Max(0, total - offset)));
            for (int i = offset; i < total && page.Count < limit; i++)
                page.Add(entry.Rows[i]);

            bool hasMore = offset + page.Count < total;

            return Json(new
            {
                Success = true,
                Source = sourceLabel,
                Env = envLabel,
                DateFrom = req.DateFrom,
                DateTo = req.DateTo,
                Plant = req.Plant,
                Vendor = req.Vendor,
                Article = req.Article,
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

        protected static string ResolveEnv(string env, out RfcConfigParameters rfcPar, out string envLabel)
        {
            string envNorm = (env ?? "prod").Trim().ToLowerInvariant();
            switch (envNorm)
            {
                case "dev":
                case "development":
                    rfcPar = BaseController.rfcConfigparameters();
                    envLabel = "dev";
                    return null;
                case "qa":
                case "quality":
                    rfcPar = BaseController.rfcConfigparametersquality();
                    envLabel = "qa";
                    return null;
                case "prod":
                case "production":
                case "":
                    rfcPar = BaseController.rfcConfigparametersproduction();
                    envLabel = "prod";
                    return null;
                default:
                    rfcPar = null;
                    envLabel = null;
                    return "Invalid env '" + env + "'. Use dev | qa | prod.";
            }
        }

        protected bool IsAuthorized()
        {
            IEnumerable<string> headers;
            if (!Request.Headers.TryGetValues("X-RFC-Key", out headers)) return false;
            foreach (var h in headers)
                if (string.Equals(h, API_KEY, StringComparison.Ordinal)) return true;
            return false;
        }

        protected static DateTime ParseIsoDate(string s, string fieldName)
        {
            DateTime dt;
            if (!DateTime.TryParseExact((s ?? "").Trim(), new[] { "yyyy-MM-dd", "yyyyMMdd" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                throw new FormatException(fieldName + " '" + s + "' must be YYYY-MM-DD.");
            }
            return dt;
        }

        protected static string TrimOrNull(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        protected static string SafeGet(IRfcStructure row, string field)
        {
            try { return row.GetString(field); } catch { return null; }
        }

        protected static string BuildCacheKey(string source, string env, ReadRequest req)
        {
            string raw = source + "|" + env + "|" + (req.DateFrom ?? "") + "|" + (req.DateTo ?? "") + "|" +
                         (req.Plant ?? "") + "|" + (req.Vendor ?? "") + "|" + (req.Article ?? "");
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return Convert.ToBase64String(hash);
            }
        }

        protected static void PruneExpired(ConcurrentDictionary<string, CacheEntry> cache)
        {
            if (cache.Count < 64) return;
            DateTime now = DateTime.UtcNow;
            foreach (var kv in cache)
            {
                if (kv.Value.ExpiresAt <= now)
                {
                    CacheEntry ignored;
                    cache.TryRemove(kv.Key, out ignored);
                }
            }
        }
    }
}
