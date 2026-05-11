using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.NSO
{
    /// <summary>
    /// Unified ECOMM Order Update — Head + Address + Line Items (atomic).
    /// RFC: Z_ECOMM_UPDATE_ORDER
    /// Tables: ZECOMM_ORD_HEAD, ZECOMM_ADD, ZECOMM_ORD_ITEM
    /// Import: IV_SALES_ORDER_CODE + IS_ORDER_HEAD (struct) + IS_ORDER_ADDR (struct)
    /// Tables in: IT_ORDER_ITEMS. Tables out: ET_MESSAGES (BAPIRET2).
    /// Export: EV_RETURN_CODE, EV_MESSAGE.
    /// </summary>
    [RoutePrefix("api")]
    public class Z_ECOMM_UPDATE_ORDERController : BaseController
    {
        [HttpPost, Route("Z_ECOMM_UPDATE_ORDER")]
        public HttpResponseMessage Execute([FromBody] EcommUpdateOrderRequest request)
        {
            try
            {
                if (request == null) request = new EcommUpdateOrderRequest();
                if (string.IsNullOrWhiteSpace(request.IV_SALES_ORDER_CODE))
                    return Request.CreateResponse(HttpStatusCode.OK,
                        new { Status = "E", Message = "IV_SALES_ORDER_CODE is required" });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("Z_ECOMM_UPDATE_ORDER");

                // ── Scalar import ────────────────────────────────────────────
                myfun.SetValue("IV_SALES_ORDER_CODE", request.IV_SALES_ORDER_CODE ?? "");

                // ── IS_ORDER_HEAD (structure) ────────────────────────────────
                if (request.IS_ORDER_HEAD != null)
                {
                    IRfcStructure head = myfun.GetStructure("IS_ORDER_HEAD");
                    var h = request.IS_ORDER_HEAD;
                    head.SetValue("DISPLAY_ORDER_CODE",   h.DISPLAY_ORDER_CODE   ?? "");
                    head.SetValue("ST_CD",                h.ST_CD                ?? "");
                    head.SetValue("DISPLAY_ORDER_DATE",   h.DISPLAY_ORDER_DATE   ?? "");
                    head.SetValue("DISPLAY_ORDER_TIME",   h.DISPLAY_ORDER_TIME   ?? "");
                    head.SetValue("CUSTOMER_CODE",        h.CUSTOMER_CODE        ?? "");
                    head.SetValue("CUSTOMER_NAME",        h.CUSTOMER_NAME        ?? "");
                    head.SetValue("CUSTOMER_GST_NO",      h.CUSTOMER_GST_NO      ?? "");
                    head.SetValue("CHANNEL",              h.CHANNEL              ?? "");
                    head.SetValue("NOTIFICATION_EMAIL",   h.NOTIFICATION_EMAIL   ?? "");
                    head.SetValue("NOTIFICATION_MOBILE",  h.NOTIFICATION_MOBILE  ?? "");
                    head.SetValue("CASH_ON_DELIVERY",     h.CASH_ON_DELIVERY     ?? "");
                    head.SetValue("PAYMENT_INSTRUMENT",   h.PAYMENT_INSTRUMENT   ?? "");
                    if (h.SHIPPING_CHARGES.HasValue) head.SetValue("SHIPPING_CHARGES", h.SHIPPING_CHARGES.Value);
                    if (h.GIFTWRAP_CHARGES.HasValue) head.SetValue("GIFTWRAP_CHARGES", h.GIFTWRAP_CHARGES.Value);
                    if (h.COD_CHARGES.HasValue)      head.SetValue("COD_CHARGES",      h.COD_CHARGES.Value);
                    head.SetValue("PICK_FROM_STORE",      h.PICK_FROM_STORE      ?? "");
                    head.SetValue("PREIMIMIM_DELIVERY",   h.PREIMIMIM_DELIVERY   ?? "");
                    head.SetValue("CUSTOM_FIELD_VALUES2", h.CUSTOM_FIELD_VALUES2 ?? "");
                    head.SetValue("CUSTOM_FIELD_NAME3",   h.CUSTOM_FIELD_NAME3   ?? "");
                    head.SetValue("CUSTOM_FIELD_VALUES3", h.CUSTOM_FIELD_VALUES3 ?? "");
                }

                // ── IS_ORDER_ADDR (structure) ────────────────────────────────
                if (request.IS_ORDER_ADDR != null)
                {
                    IRfcStructure addr = myfun.GetStructure("IS_ORDER_ADDR");
                    var a = request.IS_ORDER_ADDR;
                    addr.SetValue("BILL_TO_NAME1",        a.BILL_TO_NAME1        ?? "");
                    addr.SetValue("BILL_TO_NAME2",        a.BILL_TO_NAME2        ?? "");
                    addr.SetValue("BILL_TO_ADDRESSLINE1", a.BILL_TO_ADDRESSLINE1 ?? "");
                    addr.SetValue("BILL_TO_ADDRESSLINE2", a.BILL_TO_ADDRESSLINE2 ?? "");
                    addr.SetValue("BILL_TO_LATITUDE",     a.BILL_TO_LATITUDE     ?? "");
                    addr.SetValue("BILL_TO_LONGITUDE",    a.BILL_TO_LONGITUDE    ?? "");
                    addr.SetValue("BILL_TO_CITY",         a.BILL_TO_CITY         ?? "");
                    addr.SetValue("BILL_TO_STATE",        a.BILL_TO_STATE        ?? "");
                    addr.SetValue("BILL_TO_COUNTRY",      a.BILL_TO_COUNTRY      ?? "");
                    addr.SetValue("BILL_TO_PINCODE",      a.BILL_TO_PINCODE      ?? "");
                    addr.SetValue("BILL_TO_PHONE",        a.BILL_TO_PHONE        ?? "");
                    addr.SetValue("BILL_TO_EMAIL",        a.BILL_TO_EMAIL        ?? "");
                    addr.SetValue("SHIP_TO_NAME",         a.SHIP_TO_NAME         ?? "");
                    addr.SetValue("SHIP_TO_NAME1",        a.SHIP_TO_NAME1        ?? "");
                    addr.SetValue("SHIP_TO_ADDRESSLINE1", a.SHIP_TO_ADDRESSLINE1 ?? "");
                    addr.SetValue("SHIP_TO_ADDRESSLINE2", a.SHIP_TO_ADDRESSLINE2 ?? "");
                    addr.SetValue("SHIP_TO_LATITUDE",     a.SHIP_TO_LATITUDE     ?? "");
                    addr.SetValue("SHIP_TO_LONGITUDE",    a.SHIP_TO_LONGITUDE    ?? "");
                    addr.SetValue("SHIP_TO_CITY",         a.SHIP_TO_CITY         ?? "");
                    addr.SetValue("SHIP_TO_STATE",        a.SHIP_TO_STATE        ?? "");
                    addr.SetValue("SHIP_TO_COUNTRY",      a.SHIP_TO_COUNTRY      ?? "");
                    addr.SetValue("SHIP_TO_PINCODE",      a.SHIP_TO_PINCODE      ?? "");
                    addr.SetValue("SHIP_TO_PHONE",        a.SHIP_TO_PHONE        ?? "");
                    addr.SetValue("SHIP_TO_EMAIL",        a.SHIP_TO_EMAIL        ?? "");
                }

                // ── IT_ORDER_ITEMS (table input) ─────────────────────────────
                if (request.IT_ORDER_ITEMS != null && request.IT_ORDER_ITEMS.Count > 0)
                {
                    IRfcTable items = myfun.GetTable("IT_ORDER_ITEMS");
                    foreach (var item in request.IT_ORDER_ITEMS)
                    {
                        items.Append();
                        items.SetValue("ITEM_NO",            item.ITEM_NO            ?? "");
                        items.SetValue("ITEM_SKU",           item.ITEM_SKU           ?? "");
                        items.SetValue("ST_CD",              item.ST_CD              ?? "");
                        if (item.MRP.HasValue)             items.SetValue("MRP",             item.MRP.Value);
                        if (item.SALE_VALUE.HasValue)      items.SetValue("SALE_VALUE",      item.SALE_VALUE.Value);
                        if (item.DISCOUNT.HasValue)        items.SetValue("DISCOUNT",        item.DISCOUNT.Value);
                        if (item.SHIPPING_CHARGES.HasValue)items.SetValue("SHIPPING_CHARGES",item.SHIPPING_CHARGES.Value);
                        items.SetValue("CGST_RATE",          item.CGST_RATE          ?? "");
                        items.SetValue("SGST_RATE",          item.SGST_RATE          ?? "");
                        items.SetValue("IGST_RATE",          item.IGST_RATE          ?? "");
                        if (item.CGST_VALUE.HasValue)      items.SetValue("CGST_VALUE",      item.CGST_VALUE.Value);
                        if (item.SGST_VALUE.HasValue)      items.SetValue("SGST_VALUE",      item.SGST_VALUE.Value);
                        if (item.IGST_VALUE.HasValue)      items.SetValue("IGST_VALUE",      item.IGST_VALUE.Value);
                        items.SetValue("CUSTOM_FIELD_NAME1",  item.CUSTOM_FIELD_NAME1  ?? "");
                        items.SetValue("CUSTOM_FIELD_VALUES1",item.CUSTOM_FIELD_VALUES1 ?? "");
                        items.SetValue("CUSTOM_FIELD_NAME2",  item.CUSTOM_FIELD_NAME2  ?? "");
                        items.SetValue("CUSTOM_FIELD_VALUES2",item.CUSTOM_FIELD_VALUES2 ?? "");
                        items.SetValue("CUSTOM_FIELD_NAME3",  item.CUSTOM_FIELD_NAME3  ?? "");
                        items.SetValue("CUSTOM_FIELD_VALUES3",item.CUSTOM_FIELD_VALUES3 ?? "");
                    }
                }

                // ── Invoke ───────────────────────────────────────────────────
                myfun.Invoke(dest);

                // ── Read exports ─────────────────────────────────────────────
                string returnCode = myfun.GetString("EV_RETURN_CODE");
                string message    = myfun.GetString("EV_MESSAGE");

                // ── Read ET_MESSAGES (BAPIRET2 table) ────────────────────────
                IRfcTable msgTable = myfun.GetTable("ET_MESSAGES");
                var messages = new List<object>();
                foreach (IRfcStructure row in msgTable)
                {
                    messages.Add(new
                    {
                        TYPE    = row.GetString("TYPE"),
                        ID      = row.GetString("ID"),
                        NUMBER  = row.GetString("NUMBER"),
                        MESSAGE = row.GetString("MESSAGE")
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status  = returnCode,
                    Message = message,
                    Data    = new { ET_MESSAGES = messages }
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

    // ── Request Models ───────────────────────────────────────────────────────

    public class EcommUpdateOrderRequest
    {
        public string IV_SALES_ORDER_CODE { get; set; }
        public OrderHeadModel IS_ORDER_HEAD { get; set; }
        public OrderAddrModel IS_ORDER_ADDR { get; set; }
        public List<OrderItemModel> IT_ORDER_ITEMS { get; set; }
    }

    public class OrderHeadModel
    {
        public string DISPLAY_ORDER_CODE   { get; set; }
        public string ST_CD                { get; set; }
        public string DISPLAY_ORDER_DATE   { get; set; }  // yyyyMMdd
        public string DISPLAY_ORDER_TIME   { get; set; }  // HHmmss
        public string CUSTOMER_CODE        { get; set; }
        public string CUSTOMER_NAME        { get; set; }
        public string CUSTOMER_GST_NO      { get; set; }
        public string CHANNEL              { get; set; }
        public string NOTIFICATION_EMAIL   { get; set; }
        public string NOTIFICATION_MOBILE  { get; set; }
        public string CASH_ON_DELIVERY     { get; set; }
        public string PAYMENT_INSTRUMENT   { get; set; }
        public decimal? SHIPPING_CHARGES   { get; set; }
        public decimal? GIFTWRAP_CHARGES   { get; set; }
        public decimal? COD_CHARGES        { get; set; }
        public string PICK_FROM_STORE      { get; set; }
        public string PREIMIMIM_DELIVERY   { get; set; }
        public string CUSTOM_FIELD_VALUES2 { get; set; }
        public string CUSTOM_FIELD_NAME3   { get; set; }
        public string CUSTOM_FIELD_VALUES3 { get; set; }
    }

    public class OrderAddrModel
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
        public string BILL_TO_PINCODE      { get; set; }
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
        public string SHIP_TO_PINCODE      { get; set; }
        public string SHIP_TO_PHONE        { get; set; }
        public string SHIP_TO_EMAIL        { get; set; }
    }

    public class OrderItemModel
    {
        public string  ITEM_NO             { get; set; }
        public string  ITEM_SKU            { get; set; }
        public string  ST_CD               { get; set; }
        public decimal? MRP                { get; set; }
        public decimal? SALE_VALUE         { get; set; }
        public decimal? DISCOUNT           { get; set; }
        public decimal? SHIPPING_CHARGES   { get; set; }
        public string  CGST_RATE           { get; set; }
        public string  SGST_RATE           { get; set; }
        public string  IGST_RATE           { get; set; }
        public decimal? CGST_VALUE         { get; set; }
        public decimal? SGST_VALUE         { get; set; }
        public decimal? IGST_VALUE         { get; set; }
        public string  CUSTOM_FIELD_NAME1  { get; set; }
        public string  CUSTOM_FIELD_VALUES1{ get; set; }
        public string  CUSTOM_FIELD_NAME2  { get; set; }
        public string  CUSTOM_FIELD_VALUES2{ get; set; }
        public string  CUSTOM_FIELD_NAME3  { get; set; }
        public string  CUSTOM_FIELD_VALUES3{ get; set; }
    }
}
