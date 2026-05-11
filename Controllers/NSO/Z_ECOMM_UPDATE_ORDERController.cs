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
    /// Updates: ZECOMM_ORD_HEAD, ZECOMM_ADD, ZECOMM_ORD_ITEM in one commit.
    /// All IS_ORDER_HEAD and IS_ORDER_ADDR fields are flat in the request body.
    /// IT_ORDER_ITEMS is an array for the line items table.
    /// </summary>
    [RoutePrefix("api")]
    public class Z_ECOMM_UPDATE_ORDERController : BaseController
    {
        [HttpPost, Route("Z_ECOMM_UPDATE_ORDER")]
        public HttpResponseMessage Execute([FromBody] Z_ECOMM_UPDATE_ORDERRequest request)
        {
            try
            {
                if (request == null) request = new Z_ECOMM_UPDATE_ORDERRequest();
                if (string.IsNullOrWhiteSpace(request.IV_SALES_ORDER_CODE))
                    return Request.CreateResponse(HttpStatusCode.OK,
                        new { Status = "E", Message = "IV_SALES_ORDER_CODE is required" });

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction myfun = dest.Repository.CreateFunction("Z_ECOMM_UPDATE_ORDER");

                // ── Scalar import ────────────────────────────────────────────
                myfun.SetValue("IV_SALES_ORDER_CODE", request.IV_SALES_ORDER_CODE ?? "");

                // ── IS_ORDER_HEAD — flat fields mapped to SAP structure ───────
                IRfcStructure head = myfun.GetStructure("IS_ORDER_HEAD");
                head.SetValue("DISPLAY_ORDER_CODE",   request.DISPLAY_ORDER_CODE   ?? "");
                head.SetValue("ST_CD",                request.ST_CD                ?? "");
                head.SetValue("DISPLAY_ORDER_DATE",   request.DISPLAY_ORDER_DATE   ?? "");
                head.SetValue("DISPLAY_ORDER_TIME",   request.DISPLAY_ORDER_TIME   ?? "");
                head.SetValue("CUSTOMER_CODE",        request.CUSTOMER_CODE        ?? "");
                head.SetValue("CUSTOMER_NAME",        request.CUSTOMER_NAME        ?? "");
                head.SetValue("CUSTOMER_GST_NO",      request.CUSTOMER_GST_NO      ?? "");
                head.SetValue("CHANNEL",              request.CHANNEL              ?? "");
                head.SetValue("NOTIFICATION_EMAIL",   request.NOTIFICATION_EMAIL   ?? "");
                head.SetValue("NOTIFICATION_MOBILE",  request.NOTIFICATION_MOBILE  ?? "");
                head.SetValue("CASH_ON_DELIVERY",     request.CASH_ON_DELIVERY     ?? "");
                head.SetValue("PAYMENT_INSTRUMENT",   request.PAYMENT_INSTRUMENT   ?? "");
                if (request.SHIPPING_CHARGES.HasValue)
                    head.SetValue("SHIPPING_CHARGES", request.SHIPPING_CHARGES.Value);
                if (request.GIFTWRAP_CHARGES.HasValue)
                    head.SetValue("GIFTWRAP_CHARGES", request.GIFTWRAP_CHARGES.Value);
                if (request.COD_CHARGES.HasValue)
                    head.SetValue("COD_CHARGES",      request.COD_CHARGES.Value);
                head.SetValue("PICK_FROM_STORE",      request.PICK_FROM_STORE      ?? "");
                head.SetValue("PREIMIMIM_DELIVERY",   request.PREIMIMIM_DELIVERY   ?? "");
                head.SetValue("CUSTOM_FIELD_VALUES2", request.CUSTOM_FIELD_VALUES2 ?? "");
                head.SetValue("CUSTOM_FIELD_NAME3",   request.CUSTOM_FIELD_NAME3   ?? "");
                head.SetValue("CUSTOM_FIELD_VALUES3", request.CUSTOM_FIELD_VALUES3 ?? "");

                // ── IS_ORDER_ADDR — flat fields mapped to SAP structure ───────
                IRfcStructure addr = myfun.GetStructure("IS_ORDER_ADDR");
                addr.SetValue("BILL_TO_NAME1",        request.BILL_TO_NAME1        ?? "");
                addr.SetValue("BILL_TO_NAME2",        request.BILL_TO_NAME2        ?? "");
                addr.SetValue("BILL_TO_ADDRESSLINE1", request.BILL_TO_ADDRESSLINE1 ?? "");
                addr.SetValue("BILL_TO_ADDRESSLINE2", request.BILL_TO_ADDRESSLINE2 ?? "");
                addr.SetValue("BILL_TO_LATITUDE",     request.BILL_TO_LATITUDE     ?? "");
                addr.SetValue("BILL_TO_LONGITUDE",    request.BILL_TO_LONGITUDE    ?? "");
                addr.SetValue("BILL_TO_CITY",         request.BILL_TO_CITY         ?? "");
                addr.SetValue("BILL_TO_STATE",        request.BILL_TO_STATE        ?? "");
                addr.SetValue("BILL_TO_COUNTRY",      request.BILL_TO_COUNTRY      ?? "");
                addr.SetValue("BILL_TO_PINCODE",      request.BILL_TO_PINCODE      ?? "");
                addr.SetValue("BILL_TO_PHONE",        request.BILL_TO_PHONE        ?? "");
                addr.SetValue("BILL_TO_EMAIL",        request.BILL_TO_EMAIL        ?? "");
                addr.SetValue("SHIP_TO_NAME",         request.SHIP_TO_NAME         ?? "");
                addr.SetValue("SHIP_TO_NAME1",        request.SHIP_TO_NAME1        ?? "");
                addr.SetValue("SHIP_TO_ADDRESSLINE1", request.SHIP_TO_ADDRESSLINE1 ?? "");
                addr.SetValue("SHIP_TO_ADDRESSLINE2", request.SHIP_TO_ADDRESSLINE2 ?? "");
                addr.SetValue("SHIP_TO_LATITUDE",     request.SHIP_TO_LATITUDE     ?? "");
                addr.SetValue("SHIP_TO_LONGITUDE",    request.SHIP_TO_LONGITUDE    ?? "");
                addr.SetValue("SHIP_TO_CITY",         request.SHIP_TO_CITY         ?? "");
                addr.SetValue("SHIP_TO_STATE",        request.SHIP_TO_STATE        ?? "");
                addr.SetValue("SHIP_TO_COUNTRY",      request.SHIP_TO_COUNTRY      ?? "");
                addr.SetValue("SHIP_TO_PINCODE",      request.SHIP_TO_PINCODE      ?? "");
                addr.SetValue("SHIP_TO_PHONE",        request.SHIP_TO_PHONE        ?? "");
                addr.SetValue("SHIP_TO_EMAIL",        request.SHIP_TO_EMAIL        ?? "");

                // ── IT_ORDER_ITEMS — table input (RULE 3: Append + SetValue) ─
                if (request.IT_ORDER_ITEMS != null && request.IT_ORDER_ITEMS.Count > 0)
                {
                    IRfcTable items = myfun.GetTable("IT_ORDER_ITEMS");
                    foreach (var item in request.IT_ORDER_ITEMS)
                    {
                        items.Append();
                        items.SetValue("ITEM_NO",             item.ITEM_NO             ?? "");
                        items.SetValue("ITEM_SKU",            item.ITEM_SKU            ?? "");
                        items.SetValue("ST_CD",               item.ST_CD               ?? "");
                        if (item.MRP.HasValue)
                            items.SetValue("MRP",             item.MRP.Value);
                        if (item.SALE_VALUE.HasValue)
                            items.SetValue("SALE_VALUE",      item.SALE_VALUE.Value);
                        if (item.DISCOUNT.HasValue)
                            items.SetValue("DISCOUNT",        item.DISCOUNT.Value);
                        if (item.SHIPPING_CHARGES.HasValue)
                            items.SetValue("SHIPPING_CHARGES",item.SHIPPING_CHARGES.Value);
                        items.SetValue("CGST_RATE",           item.CGST_RATE           ?? "");
                        items.SetValue("SGST_RATE",           item.SGST_RATE           ?? "");
                        items.SetValue("IGST_RATE",           item.IGST_RATE           ?? "");
                        if (item.CGST_VALUE.HasValue)
                            items.SetValue("CGST_VALUE",      item.CGST_VALUE.Value);
                        if (item.SGST_VALUE.HasValue)
                            items.SetValue("SGST_VALUE",      item.SGST_VALUE.Value);
                        if (item.IGST_VALUE.HasValue)
                            items.SetValue("IGST_VALUE",      item.IGST_VALUE.Value);
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

                // ── Exports ──────────────────────────────────────────────────
                string returnCode = myfun.GetString("EV_RETURN_CODE");
                string message    = myfun.GetString("EV_MESSAGE");

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

    // ── Request Model — all fields flat, matching SAP field names exactly ────

    public class Z_ECOMM_UPDATE_ORDERRequest
    {
        // ── Top-level key ──────────────────────────────────────────────────
        public string IV_SALES_ORDER_CODE  { get; set; }

        // ── IS_ORDER_HEAD fields (ZECOMM_ORD_HEAD) ────────────────────────
        public string  DISPLAY_ORDER_CODE  { get; set; }
        public string  ST_CD               { get; set; }
        public string  DISPLAY_ORDER_DATE  { get; set; }  // yyyyMMdd
        public string  DISPLAY_ORDER_TIME  { get; set; }  // HHmmss
        public string  CUSTOMER_CODE       { get; set; }
        public string  CUSTOMER_NAME       { get; set; }
        public string  CUSTOMER_GST_NO     { get; set; }
        public string  CHANNEL             { get; set; }
        public string  NOTIFICATION_EMAIL  { get; set; }
        public string  NOTIFICATION_MOBILE { get; set; }
        public string  CASH_ON_DELIVERY    { get; set; }
        public string  PAYMENT_INSTRUMENT  { get; set; }
        public decimal? SHIPPING_CHARGES   { get; set; }
        public decimal? GIFTWRAP_CHARGES   { get; set; }
        public decimal? COD_CHARGES        { get; set; }
        public string  PICK_FROM_STORE     { get; set; }
        public string  PREIMIMIM_DELIVERY  { get; set; }
        public string  CUSTOM_FIELD_VALUES2{ get; set; }
        public string  CUSTOM_FIELD_NAME3  { get; set; }
        public string  CUSTOM_FIELD_VALUES3{ get; set; }

        // ── IS_ORDER_ADDR fields (ZECOMM_ADD) ─────────────────────────────
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

        // ── IT_ORDER_ITEMS table (array, one entry per line item) ──────────
        public List<EcommOrderItem> IT_ORDER_ITEMS { get; set; }
    }

    public class EcommOrderItem
    {
        public string  ITEM_NO              { get; set; }
        public string  ITEM_SKU             { get; set; }
        public string  ST_CD                { get; set; }
        public decimal? MRP                 { get; set; }
        public decimal? SALE_VALUE          { get; set; }
        public decimal? DISCOUNT            { get; set; }
        public decimal? SHIPPING_CHARGES    { get; set; }
        public string  CGST_RATE            { get; set; }
        public string  SGST_RATE            { get; set; }
        public string  IGST_RATE            { get; set; }
        public decimal? CGST_VALUE          { get; set; }
        public decimal? SGST_VALUE          { get; set; }
        public decimal? IGST_VALUE          { get; set; }
        public string  CUSTOM_FIELD_NAME1   { get; set; }
        public string  CUSTOM_FIELD_VALUES1 { get; set; }
        public string  CUSTOM_FIELD_NAME2   { get; set; }
        public string  CUSTOM_FIELD_VALUES2 { get; set; }
        public string  CUSTOM_FIELD_NAME3   { get; set; }
        public string  CUSTOM_FIELD_VALUES3 { get; set; }
    }
}
