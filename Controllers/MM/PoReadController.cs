using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.MM
{
    /// <summary>
    /// PO Read API — direct SAP→Lovable/Snowflake bypass for purchase-order data.
    ///
    /// RFC:     ZMM_PO_DETAILS (FMODE='R' in TFDIR, verified 2026-05-27)
    /// Source:  EKKO+EKPO, returns PO header summary (1 row per purchasing doc)
    /// Output:  IT_FINAL (ZPO_RFC_TT) — 8 fields:
    ///          PURCHASING_DOC, PO_TYPE, CREATED_ON, CREATED_BY,
    ///          SUPPLIER, NET_VALUE, PO_QUANITY, PLANT
    ///
    /// Replaces DataV2 ET_ZMM_PO_DETAILS. No dependence on RFC_MASTER catalog.
    ///
    /// Endpoint: POST /api/po
    /// Auth:     X-RFC-Key: v2-rfc-proxy-2026
    /// Env:      ?env=dev|qa|prod   (default: prod)
    ///
    /// Body:
    /// {
    ///   "DateFrom": "2026-05-26",   // YYYY-MM-DD, required
    ///   "DateTo":   "2026-05-26",   // YYYY-MM-DD, optional (defaults to DateFrom)
    ///   "Plant":    "DH24",         // optional client-side filter
    ///   "Vendor":   "0000200001",   // optional client-side filter
    ///   "Limit":    5000            // optional cap on Rows returned (default 50000)
    /// }
    ///
    /// Response:
    /// {
    ///   "Success":  true,
    ///   "Source":   "ZMM_PO_DETAILS",
    ///   "Env":      "prod",
    ///   "DateFrom": "2026-05-26",
    ///   "DateTo":   "2026-05-26",
    ///   "RowCount": 42,
    ///   "Rows":     [ { "PurchasingDoc":"4500000000", "PoType":"NB", ... } ]
    /// }
    ///
    /// Notes:
    /// - SAP date sent as YYYYMMDD via IT_CREATED_LOW / IT_CREATED_HIGH (ERDAT range).
    /// - FM returns EX_RETURN.TYPE='E' MESSAGE='No Data Found' even on success
    ///   when zero matching rows; we treat IT_FINAL.Count > 0 as success regardless.
    /// </summary>
    public class PoReadController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";
        private const int DEFAULT_LIMIT = 50000;

        public class PoReadRequest
        {
            public string DateFrom { get; set; }
            public string DateTo { get; set; }
            public string Plant { get; set; }
            public string Vendor { get; set; }
            public int? Limit { get; set; }
        }

        [HttpPost]
        [Route("api/po")]
        public IHttpActionResult Post([FromBody] PoReadRequest req, string env = "prod")
        {
            if (!IsAuthorized())
                return Json(new { Success = false, Error = "Unauthorized — missing or invalid X-RFC-Key" });

            if (req == null || string.IsNullOrWhiteSpace(req.DateFrom))
                return Json(new { Success = false, Error = "DateFrom (YYYY-MM-DD) is required." });

            string dateTo = string.IsNullOrWhiteSpace(req.DateTo) ? req.DateFrom : req.DateTo;

            string sapFrom, sapTo;
            try
            {
                sapFrom = ToSapDate(req.DateFrom);
                sapTo = ToSapDate(dateTo);
            }
            catch (FormatException fx)
            {
                return Json(new { Success = false, Error = fx.Message });
            }

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

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("ZMM_PO_DETAILS");

                // IT_CREATED_LOW / IT_CREATED_HIGH are typed as IMPORT structure ERDAT in metadata
                // but the proxy successfully sends them as scalar strings (verified 2026-05-27).
                fn.SetValue("IT_CREATED_LOW", sapFrom);
                fn.SetValue("IT_CREATED_HIGH", sapTo);

                fn.Invoke(dest);

                IRfcTable rows = fn.GetTable("IT_FINAL");
                var output = new List<object>(Math.Min(rows.RowCount, limit));
                string plantFilter = string.IsNullOrWhiteSpace(req.Plant) ? null : req.Plant.Trim();
                string vendorFilter = string.IsNullOrWhiteSpace(req.Vendor) ? null : req.Vendor.Trim().TrimStart('0');

                foreach (IRfcStructure row in rows)
                {
                    if (output.Count >= limit) break;

                    string plant = SafeGet(row, "PLANT");
                    if (plantFilter != null && !string.Equals(plant, plantFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string supplier = SafeGet(row, "SUPPLIER");
                    if (vendorFilter != null && !string.Equals((supplier ?? "").TrimStart('0'), vendorFilter, StringComparison.OrdinalIgnoreCase))
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
                string retType = ret.GetString("TYPE") ?? "";
                string retMsg = ret.GetString("MESSAGE") ?? "";

                // FM returns 'No Data Found' even when IT_FINAL has rows — only fail when both true.
                bool isFailure = retType.Equals("E", StringComparison.OrdinalIgnoreCase) && rows.RowCount == 0;

                return Json(new
                {
                    Success = !isFailure,
                    Source = "ZMM_PO_DETAILS",
                    Env = envLabel,
                    DateFrom = req.DateFrom,
                    DateTo = dateTo,
                    SapMessage = string.IsNullOrEmpty(retMsg) ? null : retMsg,
                    TotalRowsFromSap = rows.RowCount,
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

        private static string ToSapDate(string isoDate)
        {
            DateTime dt;
            if (!DateTime.TryParseExact((isoDate ?? "").Trim(), new[] { "yyyy-MM-dd", "yyyyMMdd" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                throw new FormatException("Date '" + isoDate + "' must be YYYY-MM-DD.");
            }
            return dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string SafeGet(IRfcStructure row, string field)
        {
            try { return row.GetString(field); } catch { return null; }
        }
    }
}
