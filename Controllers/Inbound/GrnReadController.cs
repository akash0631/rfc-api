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
    /// RFC:     ZFI_GRC_DETAILS_RFC (FMODE='R', server-side date filter on CPUDT)
    /// Source:  MKPF+MSEG goods-receipt postings (TRANS_EV_TYPE='WE')
    /// Output:  IT_DATA (ZFI_GRC_DETAILS_TT) — 17 fields:
    ///          MATERIAL_DOC, MAT_DOC_YEAR, MOVEMENT_TYPE, PLANT, SUPPLIER,
    ///          DEBIT_CREDIT, AMOUNT_IN_LC, QUANTITY, BASE_UNIT, PURCHASE_ORDER,
    ///          REFERENCE_DOC, SUPPLIER_RECEIVE, TRANS_EV_TYPE, POSTING_DATE,
    ///          ENTERED_ON, TEXT, MOVEMENT_WM
    ///
    /// Why ZFI_GRC_DETAILS_RFC and not ZPBI_GRC_DETAILS:
    ///   ZPBI streams full MSEG history (no IMPORT date param). PROD calls hit
    ///   Cloudflare 524 at 125s. ZFI accepts IM_ENTERED_LOW/HIGH and filters
    ///   server-side. Bonus: ZFI exposes PURCHASE_ORDER, letting Lovable join
    ///   PO ↔ GRN without a second call.
    ///
    /// Endpoint: POST /api/grn
    /// Auth:     X-RFC-Key: v2-rfc-proxy-2026
    /// Env:      ?env=dev|qa|prod   (default: prod)
    ///
    /// Body:
    /// {
    ///   "DateFrom":     "2026-05-26",   // YYYY-MM-DD, required (CPUDT >= filter, SAP-side)
    ///   "DateTo":       "2026-05-26",   // YYYY-MM-DD, optional (defaults to DateFrom)
    ///   "Plant":        "DH24",         // optional WERKS filter (client-side)
    ///   "Vendor":       "0000200001",   // optional SUPPLIER filter (client-side, zero-strip)
    ///   "MovementType": "101",          // optional BWART filter (101=GR, 102=GR-reverse)
    ///   "PurchaseOrder":"5100197585",   // optional EBELN filter (client-side)
    ///   "Limit":        5000            // optional cap (default 50000)
    /// }
    ///
    /// Response:
    /// {
    ///   "Success":  true,
    ///   "Source":   "ZFI_GRC_DETAILS_RFC",
    ///   "Env":      "prod",
    ///   "RowCount": 412,
    ///   "Rows":     [ { "MaterialDoc":"5007624703", "MovementType":"101",
    ///                   "Plant":"DW01", "PurchaseOrder":"6100002263", ... } ]
    /// }
    /// </summary>
    public class GrnReadController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";
        private const int DEFAULT_LIMIT = 50000;

        public class GrnReadRequest
        {
            public string DateFrom { get; set; }
            public string DateTo { get; set; }
            public string Plant { get; set; }
            public string Vendor { get; set; }
            public string MovementType { get; set; }
            public string PurchaseOrder { get; set; }
            public int? Limit { get; set; }
        }

        [HttpPost]
        [Route("api/grn")]
        public IHttpActionResult Post([FromBody] GrnReadRequest req, string env = "prod")
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
            string plantFilter = TrimOrNull(req.Plant);
            string vendorFilter = TrimOrNull(req.Vendor);
            string vendorMatch = vendorFilter == null ? null : vendorFilter.TrimStart('0');
            string mvtFilter = TrimOrNull(req.MovementType);
            string poFilter = TrimOrNull(req.PurchaseOrder);
            string poMatch = poFilter == null ? null : poFilter.TrimStart('0');

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("ZFI_GRC_DETAILS_RFC");
                fn.SetValue("IM_ENTERED_LOW", sapFrom);
                fn.SetValue("IM_ENTERED_HIGH", sapTo);
                fn.Invoke(dest);

                IRfcTable rows = fn.GetTable("IT_DATA");
                int totalFromSap = rows.RowCount;
                var output = new List<object>(Math.Min(totalFromSap, limit));

                foreach (IRfcStructure row in rows)
                {
                    if (output.Count >= limit) break;

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

        private static string TrimOrNull(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static string SafeGet(IRfcStructure row, string field)
        {
            try { return row.GetString(field); } catch { return null; }
        }
    }
}
