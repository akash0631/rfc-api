using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inbound
{
    /// <summary>
    /// GRN/GRC Read API — direct SAP→Lovable/Snowflake bypass for goods-receipt data.
    ///
    /// RFC:     ZPBI_GRC_DETAILS (FMODE='R' in TFDIR, verified 2026-05-27)
    /// Source:  MSEG+MKPF goods-movement data scoped to GRN/GRC postings
    /// Output:  ET_GRC_DATA — 19 fields per row:
    ///          MBLNR, MJAHR, ZEILE, BUDAT, BWART, MATNR, WERKS, CHARG, LGORT,
    ///          LIFNR, SHKZG, WAERS, DMBTR, MENGE, MEINS, MATKL, BISMT, ATTYP, PPK_QTY
    ///
    /// Replaces DataV2 ET_GRC_DATA. No dependence on RFC_MASTER catalog.
    ///
    /// Endpoint: POST /api/grn
    /// Auth:     X-RFC-Key: v2-rfc-proxy-2026
    /// Env:      ?env=dev|qa|prod   (default: prod)
    ///
    /// Body (ALL fields optional — RFC has no required IMPORT params):
    /// {
    ///   "DateFrom":     "2026-05-26",   // YYYY-MM-DD, server-side BUDAT >= filter
    ///   "DateTo":       "2026-05-26",   // YYYY-MM-DD, server-side BUDAT <= filter
    ///   "Plant":        "DH24",         // optional WERKS filter
    ///   "Vendor":       "0000200001",   // optional LIFNR filter
    ///   "MovementType": "101",          // optional BWART filter (101=GR, 102=GR-reverse)
    ///   "Limit":        5000            // optional cap (default 100000)
    /// }
    ///
    /// Response:
    /// {
    ///   "Success":  true,
    ///   "Source":   "ZPBI_GRC_DETAILS",
    ///   "Env":      "prod",
    ///   "RowCount": 412,
    ///   "Rows":     [ { "MaterialDoc":"5000000001", "Year":"2021", "Line":"0001",
    ///                   "PostingDate":"2021-09-03", "MovementType":"101", ... } ]
    /// }
    ///
    /// Notes:
    /// - FM streams the full ET_GRC_DATA table (no SAP-side date param). We filter
    ///   client-side. For large date ranges expect 5-30s response. Use Plant/Vendor
    ///   to narrow before calling Lovable for fast UX.
    /// - SHKZG: 'S' = receipt (positive), 'H' = reversal (negative).
    /// </summary>
    public class GrnReadController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";
        private const int DEFAULT_LIMIT = 100000;

        public class GrnReadRequest
        {
            public string DateFrom { get; set; }
            public string DateTo { get; set; }
            public string Plant { get; set; }
            public string Vendor { get; set; }
            public string MovementType { get; set; }
            public int? Limit { get; set; }
        }

        [HttpPost]
        [Route("api/grn")]
        public IHttpActionResult Post([FromBody] GrnReadRequest req, string env = "prod")
        {
            if (!IsAuthorized())
                return Json(new { Success = false, Error = "Unauthorized — missing or invalid X-RFC-Key" });

            req = req ?? new GrnReadRequest();

            DateTime? fromDt = ParseOptional(req.DateFrom, out string fromErr);
            if (fromErr != null) return Json(new { Success = false, Error = fromErr });
            DateTime? toDt = ParseOptional(req.DateTo, out string toErr);
            if (toErr != null) return Json(new { Success = false, Error = toErr });

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

            int limit = (req.Limit.HasValue && req.Limit.Value > 0) ? req.Limit.Value : DEFAULT_LIMIT;
            string plantFilter = Trim(req.Plant);
            string vendorFilter = string.IsNullOrEmpty(Trim(req.Vendor)) ? null : Trim(req.Vendor).TrimStart('0');
            string mvtFilter = Trim(req.MovementType);

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("ZPBI_GRC_DETAILS");
                fn.Invoke(dest);

                IRfcTable rows = fn.GetTable("ET_GRC_DATA");
                int totalFromSap = rows.RowCount;
                var output = new List<object>(Math.Min(totalFromSap, limit));

                foreach (IRfcStructure row in rows)
                {
                    if (output.Count >= limit) break;

                    string budatRaw = SafeGet(row, "BUDAT");
                    DateTime budat;
                    bool budatParsed = DateTime.TryParseExact(budatRaw, new[] { "yyyy-MM-dd", "yyyyMMdd" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out budat);

                    if (fromDt.HasValue && budatParsed && budat < fromDt.Value) continue;
                    if (toDt.HasValue && budatParsed && budat > toDt.Value) continue;

                    string werks = SafeGet(row, "WERKS");
                    if (plantFilter != null && !string.Equals(werks, plantFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string lifnr = SafeGet(row, "LIFNR");
                    if (vendorFilter != null && !string.Equals((lifnr ?? "").TrimStart('0'), vendorFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string bwart = SafeGet(row, "BWART");
                    if (mvtFilter != null && !string.Equals(bwart, mvtFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    output.Add(new
                    {
                        MaterialDoc = SafeGet(row, "MBLNR"),
                        Year = SafeGet(row, "MJAHR"),
                        Line = SafeGet(row, "ZEILE"),
                        PostingDate = budatRaw,
                        MovementType = bwart,
                        Material = SafeGet(row, "MATNR"),
                        Plant = werks,
                        Batch = SafeGet(row, "CHARG"),
                        StorageLocation = SafeGet(row, "LGORT"),
                        Vendor = lifnr,
                        DebitCredit = SafeGet(row, "SHKZG"),
                        Currency = SafeGet(row, "WAERS"),
                        Amount = SafeGet(row, "DMBTR"),
                        Quantity = SafeGet(row, "MENGE"),
                        Uom = SafeGet(row, "MEINS"),
                        MaterialGroup = SafeGet(row, "MATKL"),
                        OldMaterial = SafeGet(row, "BISMT"),
                        ArticleType = SafeGet(row, "ATTYP"),
                        PpkQty = SafeGet(row, "PPK_QTY")
                    });
                }

                return Json(new
                {
                    Success = true,
                    Source = "ZPBI_GRC_DETAILS",
                    Env = envLabel,
                    DateFrom = req.DateFrom,
                    DateTo = req.DateTo,
                    Plant = plantFilter,
                    Vendor = req.Vendor,
                    MovementType = mvtFilter,
                    TotalRowsFromSap = totalFromSap,
                    RowCount = output.Count,
                    Rows = output
                });
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

        private bool IsAuthorized()
        {
            IEnumerable<string> headers;
            if (!Request.Headers.TryGetValues("X-RFC-Key", out headers)) return false;
            foreach (var h in headers)
                if (string.Equals(h, API_KEY, StringComparison.Ordinal)) return true;
            return false;
        }

        private static DateTime? ParseOptional(string s, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(s)) return null;
            DateTime dt;
            if (!DateTime.TryParseExact(s.Trim(), new[] { "yyyy-MM-dd", "yyyyMMdd" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                error = "Date '" + s + "' must be YYYY-MM-DD.";
                return null;
            }
            return dt;
        }

        private static string Trim(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static string SafeGet(IRfcStructure row, string field)
        {
            try { return row.GetString(field); } catch { return null; }
        }
    }
}
