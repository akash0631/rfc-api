using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.NSO
{
    /// <summary>
    /// eCommerce Order POST — vendor-segregated endpoints.
    ///
    /// Posts a full order (header + address + line items) to SAP via
    /// RFC ZECOMM_ORDER_POST_RFC. Two routes exist so orders can be tracked
    /// vendor-wise: the server stamps ET_HEAD.VENDOR_NAME based on which route
    /// was called. Callers NEVER send VENDOR_NAME — it is forced server-side
    /// and any value in the request body is ignored.
    ///
    ///   POST /api/ZECOMM_ORDER_POST_UT  → VENDOR_NAME = "UT"
    ///   POST /api/ZECOMM_ORDER_POST_GH  → VENDOR_NAME = "GH"
    ///
    /// Environment: ?env=dev|qa|prod (default dev).
    /// RFC params: ET_HEAD (import table), ET_ADD (import table),
    ///             ET_ITEM (tables), EX_RETURN (BAPIRET2 export).
    /// Header vendor stamp only — ET_ITEM.VENDOR_NAME is left blank.
    /// </summary>
    [RoutePrefix("api")]
    public class ZecommOrderPostController : BaseController
    {
        [HttpPost, Route("ZECOMM_ORDER_POST_UT")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage PostUt([FromBody] ZecommOrderPostRequest request)
        {
            return PostOrder(request, "UT");
        }

        [HttpPost, Route("ZECOMM_ORDER_POST_GH")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage PostGh([FromBody] ZecommOrderPostRequest request)
        {
            return PostOrder(request, "GH");
        }

        // ── Shared implementation — vendorCode is the ONLY per-endpoint difference ──
        private HttpResponseMessage PostOrder(ZecommOrderPostRequest request, string vendorCode)
        {
            try
            {
                if (request == null || request.ET_HEAD == null)
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Status = "E", Message = "ET_HEAD is required" });

                EcommOrderHead head = request.ET_HEAD;
                if (string.IsNullOrWhiteSpace(head.SALES_ORDER_CODE) ||
                    string.IsNullOrWhiteSpace(head.DISPLAY_ORDER_CODE) ||
                    string.IsNullOrWhiteSpace(head.ST_CD))
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status  = "E",
                        Message = "ET_HEAD.SALES_ORDER_CODE, DISPLAY_ORDER_CODE and ST_CD are required"
                    });

                // ── Select SAP environment (dev default) ─────────────────────
                string env = System.Web.HttpContext.Current?.Request?.QueryString["env"] ?? "dev";
                RfcConfigParameters rfcPar;
                switch (env.ToLower())
                {
                    case "prod":
                    case "production":
                        rfcPar = BaseController.rfcConfigparametersproduction();
                        env = "prod";
                        break;
                    case "qa":
                    case "quality":
                        rfcPar = BaseController.rfcConfigparametersquality();
                        env = "qa";
                        break;
                    default:
                        rfcPar = BaseController.rfcConfigparameters();
                        env = "dev";
                        break;
                }

                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("ZECOMM_ORDER_POST_RFC");

                // ── ET_HEAD — single header row; VENDOR_NAME forced server-side ──
                IRfcTable etHead = myfun.GetTable("ET_HEAD");
                etHead.Append();
                etHead.SetValue("SALES_ORDER_CODE",     head.SALES_ORDER_CODE     ?? "");
                etHead.SetValue("DISPLAY_ORDER_CODE",   head.DISPLAY_ORDER_CODE   ?? "");
                etHead.SetValue("ST_CD",                head.ST_CD                ?? "");
                etHead.SetValue("DISPLAY_ORDER_DATE",   head.DISPLAY_ORDER_DATE   ?? "");
                etHead.SetValue("DISPLAY_ORDER_TIME",   head.DISPLAY_ORDER_TIME   ?? "");
                etHead.SetValue("CUSTOMER_CODE",        head.CUSTOMER_CODE        ?? "");
                etHead.SetValue("CUSTOMER_NAME",        head.CUSTOMER_NAME        ?? "");
                etHead.SetValue("CUSTOMER_GST_NO",      head.CUSTOMER_GST_NO      ?? "");
                etHead.SetValue("CHANNEL",              head.CHANNEL              ?? "");
                etHead.SetValue("NOTIFICATION_EMAIL",   head.NOTIFICATION_EMAIL   ?? "");
                etHead.SetValue("NOTIFICATION_MOBILE",  head.NOTIFICATION_MOBILE  ?? "");
                etHead.SetValue("CASH_ON_DELIVERY",     head.CASH_ON_DELIVERY     ?? "");
                etHead.SetValue("PAYMENT_INSTRUMENT",   head.PAYMENT_INSTRUMENT   ?? "");
                if (head.SHIPPING_CHARGES.HasValue) etHead.SetValue("SHIPPING_CHARGES", head.SHIPPING_CHARGES.Value);
                if (head.GIFTWRAP_CHARGES.HasValue) etHead.SetValue("GIFTWRAP_CHARGES", head.GIFTWRAP_CHARGES.Value);
                if (head.COD_CHARGES.HasValue)      etHead.SetValue("COD_CHARGES",      head.COD_CHARGES.Value);
                etHead.SetValue("PICK_FROM_STORE",      head.PICK_FROM_STORE      ?? "");
                etHead.SetValue("PREIMIMIM_DELIVERY",   head.PREIMIMIM_DELIVERY   ?? "");
                etHead.SetValue("CUSTOM_FIELD_VALUES2", head.CUSTOM_FIELD_VALUES2 ?? "");
                etHead.SetValue("CUSTOM_FIELD_NAME3",   head.CUSTOM_FIELD_NAME3   ?? "");
                etHead.SetValue("CUSTOM_FIELD_VALUES3", head.CUSTOM_FIELD_VALUES3 ?? "");
                etHead.SetValue("VENDOR_NAME",          vendorCode);   // ← hardcoded per endpoint

                // ── ET_ADD — single bill/ship address row (optional) ─────────
                if (request.ET_ADD != null)
                {
                    EcommOrderAddr a = request.ET_ADD;
                    IRfcTable etAdd = myfun.GetTable("ET_ADD");
                    etAdd.Append();
                    etAdd.SetValue("BILL_TO_NAME1",        a.BILL_TO_NAME1        ?? "");
                    etAdd.SetValue("BILL_TO_NAME2",        a.BILL_TO_NAME2        ?? "");
                    etAdd.SetValue("BILL_TO_ADDRESSLINE1", a.BILL_TO_ADDRESSLINE1 ?? "");
                    etAdd.SetValue("BILL_TO_ADDRESSLINE2", a.BILL_TO_ADDRESSLINE2 ?? "");
                    etAdd.SetValue("BILL_TO_LATITUDE",     a.BILL_TO_LATITUDE     ?? "");
                    etAdd.SetValue("BILL_TO_LONGITUDE",    a.BILL_TO_LONGITUDE    ?? "");
                    etAdd.SetValue("BILL_TO_CITY",         a.BILL_TO_CITY         ?? "");
                    etAdd.SetValue("BILL_TO_STATE",        a.BILL_TO_STATE        ?? "");
                    etAdd.SetValue("BILL_TO_COUNTRY",      a.BILL_TO_COUNTRY      ?? "");
                    etAdd.SetValue("BILL_TO_PINCODE",      a.BILL_TO_PINCODE      ?? "");
                    etAdd.SetValue("BILL_TO_PHONE",        a.BILL_TO_PHONE        ?? "");
                    etAdd.SetValue("BILL_TO_EMAIL",        a.BILL_TO_EMAIL        ?? "");
                    etAdd.SetValue("SHIP_TO_NAME",         a.SHIP_TO_NAME         ?? "");
                    etAdd.SetValue("SHIP_TO_NAME1",        a.SHIP_TO_NAME1        ?? "");
                    etAdd.SetValue("SHIP_TO_ADDRESSLINE1", a.SHIP_TO_ADDRESSLINE1 ?? "");
                    etAdd.SetValue("SHIP_TO_ADDRESSLINE2", a.SHIP_TO_ADDRESSLINE2 ?? "");
                    etAdd.SetValue("SHIP_TO_LATITUDE",     a.SHIP_TO_LATITUDE     ?? "");
                    etAdd.SetValue("SHIP_TO_LONGITUDE",    a.SHIP_TO_LONGITUDE    ?? "");
                    etAdd.SetValue("SHIP_TO_CITY",         a.SHIP_TO_CITY         ?? "");
                    etAdd.SetValue("SHIP_TO_STATE",        a.SHIP_TO_STATE        ?? "");
                    etAdd.SetValue("SHIP_TO_COUNTRY",      a.SHIP_TO_COUNTRY      ?? "");
                    etAdd.SetValue("SHIP_TO_PINCODE",      a.SHIP_TO_PINCODE      ?? "");
                    etAdd.SetValue("SHIP_TO_PHONE",        a.SHIP_TO_PHONE        ?? "");
                    etAdd.SetValue("SHIP_TO_EMAIL",        a.SHIP_TO_EMAIL        ?? "");
                }

                // ── ET_ITEM — line items (VENDOR_NAME left blank: header-only stamp) ──
                if (request.ET_ITEM != null && request.ET_ITEM.Count > 0)
                {
                    IRfcTable etItem = myfun.GetTable("ET_ITEM");
                    foreach (EcommOrderItemRow it in request.ET_ITEM)
                    {
                        etItem.Append();
                        etItem.SetValue("ITEM_NO",  it.ITEM_NO  ?? "");
                        etItem.SetValue("ITEM_SKU", it.ITEM_SKU ?? "");
                        etItem.SetValue("ST_CD",    it.ST_CD    ?? "");
                        if (it.MRP.HasValue)              etItem.SetValue("MRP",              it.MRP.Value);
                        if (it.SALE_VALUE.HasValue)       etItem.SetValue("SALE_VALUE",       it.SALE_VALUE.Value);
                        if (it.DISCOUNT.HasValue)         etItem.SetValue("DISCOUNT",         it.DISCOUNT.Value);
                        if (it.SHIPPING_CHARGES.HasValue) etItem.SetValue("SHIPPING_CHARGES", it.SHIPPING_CHARGES.Value);
                        etItem.SetValue("CGST_RATE", it.CGST_RATE ?? "");
                        etItem.SetValue("SGST_RATE", it.SGST_RATE ?? "");
                        etItem.SetValue("IGST_RATE", it.IGST_RATE ?? "");
                        if (it.CGST_VALUE.HasValue) etItem.SetValue("CGST_VALUE", it.CGST_VALUE.Value);
                        if (it.SGST_VALUE.HasValue) etItem.SetValue("SGST_VALUE", it.SGST_VALUE.Value);
                        if (it.IGST_VALUE.HasValue) etItem.SetValue("IGST_VALUE", it.IGST_VALUE.Value);
                        etItem.SetValue("CUSTOM_FIELD_NAME1",   it.CUSTOM_FIELD_NAME1   ?? "");
                        etItem.SetValue("CUSTOM_FIELD_VALUES1", it.CUSTOM_FIELD_VALUES1 ?? "");
                        etItem.SetValue("CUSTOM_FIELD_NAME2",   it.CUSTOM_FIELD_NAME2   ?? "");
                        etItem.SetValue("CUSTOM_FIELD_VALUES2", it.CUSTOM_FIELD_VALUES2 ?? "");
                        etItem.SetValue("CUSTOM_FIELD_NAME3",   it.CUSTOM_FIELD_NAME3   ?? "");
                        etItem.SetValue("CUSTOM_FIELD_VALUES3", it.CUSTOM_FIELD_VALUES3 ?? "");
                    }
                }

                // ── Invoke — RFC commits internally (custom Z posting FM) ────
                myfun.Invoke(dest);

                // ── EX_RETURN is BAPIRET2 STRUCTURE → GetStructure ───────────
                IRfcStructure exReturn = myfun.GetStructure("EX_RETURN");

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status  = exReturn.GetString("TYPE"),
                    Message = exReturn.GetString("MESSAGE"),
                    Vendor  = vendorCode,
                    Env     = env,
                    Data = new
                    {
                        EX_RETURN = new
                        {
                            TYPE    = exReturn.GetString("TYPE"),
                            ID      = exReturn.GetString("ID"),
                            NUMBER  = exReturn.GetString("NUMBER"),
                            MESSAGE = exReturn.GetString("MESSAGE")
                        }
                    }
                });
            }
            catch (RfcAbapException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = "ABAP: " + ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = "RFC Comm: " + ex.Message });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = ex.Message });
            }
        }
    }

    // ── Request models — fields match the SAP structures exactly ──────────────
    // VENDOR_NAME is intentionally omitted from the header model; it is set
    // server-side per endpoint ("UT" or "GH") and cannot be overridden.

    public class ZecommOrderPostRequest
    {
        /// <summary>Order header (ZECOMM_ORD_HEAD_STR) — required.</summary>
        public EcommOrderHead ET_HEAD { get; set; }

        /// <summary>Bill-to / ship-to address (ZECOMM_ORD_ADD_STR) — optional.</summary>
        public EcommOrderAddr ET_ADD { get; set; }

        /// <summary>Line items (ZECOMM_ORD_ITEM_STR) — one entry per SKU.</summary>
        public List<EcommOrderItemRow> ET_ITEM { get; set; }
    }

    public class EcommOrderHead
    {
        /// <summary>Sales Order Code (CHAR45) — required (key)</summary>
        public string SALES_ORDER_CODE    { get; set; }
        /// <summary>Display Order Code (CHAR45) — required (key)</summary>
        public string DISPLAY_ORDER_CODE  { get; set; }
        /// <summary>Store Code (ZST_CD, CHAR4) — required (key) e.g. "B03"</summary>
        public string ST_CD               { get; set; }
        /// <summary>Order date yyyyMMdd e.g. "20260716"</summary>
        public string DISPLAY_ORDER_DATE  { get; set; }
        /// <summary>Order time HHmmss e.g. "143000"</summary>
        public string DISPLAY_ORDER_TIME  { get; set; }
        public string CUSTOMER_CODE       { get; set; }
        public string CUSTOMER_NAME       { get; set; }
        public string CUSTOMER_GST_NO     { get; set; }
        /// <summary>Channel (CHAR6) e.g. "WEB", "APP"</summary>
        public string CHANNEL             { get; set; }
        public string NOTIFICATION_EMAIL  { get; set; }
        public string NOTIFICATION_MOBILE { get; set; }
        /// <summary>"YES" or "NO"</summary>
        public string CASH_ON_DELIVERY    { get; set; }
        public string PAYMENT_INSTRUMENT  { get; set; }
        public decimal? SHIPPING_CHARGES  { get; set; }
        public decimal? GIFTWRAP_CHARGES  { get; set; }
        public decimal? COD_CHARGES       { get; set; }
        public string PICK_FROM_STORE     { get; set; }
        public string PREIMIMIM_DELIVERY  { get; set; }
        public string CUSTOM_FIELD_VALUES2 { get; set; }
        public string CUSTOM_FIELD_NAME3  { get; set; }
        public string CUSTOM_FIELD_VALUES3 { get; set; }
        // VENDOR_NAME — NOT exposed; forced to UT/GH server-side.
    }

    public class EcommOrderAddr
    {
        public string BILL_TO_NAME1        { get; set; }
        public string BILL_TO_NAME2        { get; set; }
        public string BILL_TO_ADDRESSLINE1 { get; set; }
        public string BILL_TO_ADDRESSLINE2 { get; set; }
        public string BILL_TO_LATITUDE     { get; set; }
        public string BILL_TO_LONGITUDE    { get; set; }
        public string BILL_TO_CITY         { get; set; }
        public string BILL_TO_STATE        { get; set; }
        public string BILL_TO_COUNTRY      { get; set; }
        /// <summary>Bill-to PIN (NUMC10) — digits only</summary>
        public string BILL_TO_PINCODE      { get; set; }
        /// <summary>Bill-to phone (NUMC15) — digits only</summary>
        public string BILL_TO_PHONE        { get; set; }
        public string BILL_TO_EMAIL        { get; set; }
        public string SHIP_TO_NAME         { get; set; }
        public string SHIP_TO_NAME1        { get; set; }
        public string SHIP_TO_ADDRESSLINE1 { get; set; }
        public string SHIP_TO_ADDRESSLINE2 { get; set; }
        public string SHIP_TO_LATITUDE     { get; set; }
        public string SHIP_TO_LONGITUDE    { get; set; }
        public string SHIP_TO_CITY         { get; set; }
        public string SHIP_TO_STATE        { get; set; }
        public string SHIP_TO_COUNTRY      { get; set; }
        /// <summary>Ship-to PIN (NUMC10) — digits only</summary>
        public string SHIP_TO_PINCODE      { get; set; }
        /// <summary>Ship-to phone (NUMC15) — digits only</summary>
        public string SHIP_TO_PHONE        { get; set; }
        public string SHIP_TO_EMAIL        { get; set; }
    }

    public class EcommOrderItemRow
    {
        /// <summary>Item number (NUMC2) e.g. "01"</summary>
        public string ITEM_NO              { get; set; }
        /// <summary>Item SKU / EAN (CHAR18)</summary>
        public string ITEM_SKU             { get; set; }
        public string ST_CD                { get; set; }
        public decimal? MRP                { get; set; }
        public decimal? SALE_VALUE         { get; set; }
        public decimal? DISCOUNT           { get; set; }
        public decimal? SHIPPING_CHARGES   { get; set; }
        /// <summary>CGST rate (CHAR5) e.g. "2.5"</summary>
        public string CGST_RATE            { get; set; }
        public string SGST_RATE            { get; set; }
        public string IGST_RATE            { get; set; }
        public decimal? CGST_VALUE         { get; set; }
        public decimal? SGST_VALUE         { get; set; }
        public decimal? IGST_VALUE         { get; set; }
        public string CUSTOM_FIELD_NAME1   { get; set; }
        public string CUSTOM_FIELD_VALUES1 { get; set; }
        public string CUSTOM_FIELD_NAME2   { get; set; }
        public string CUSTOM_FIELD_VALUES2 { get; set; }
        public string CUSTOM_FIELD_NAME3   { get; set; }
        public string CUSTOM_FIELD_VALUES3 { get; set; }
        // VENDOR_NAME — left blank (header-only vendor stamp).
    }
}
