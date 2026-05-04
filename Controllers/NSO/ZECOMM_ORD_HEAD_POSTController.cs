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
    /// eCommerce Order Header POST RFC.
    /// Writes order header records INTO SAP table ZECOMM_ORD_HEAD.
    /// Performs an UPSERT: inserts new records, updates existing ones (matched by key).
    ///
    /// Primary key: SALES_ORDER_CODE + DISPLAY_ORDER_CODE + ST_CD
    ///
    /// Supports:
    ///   - Bulk write: pass multiple records in IT_DATA array
    ///   - Test run: pass IV_TEST_RUN=true to validate without writing
    ///   - Partial validation: key fields (SALES_ORDER_CODE, DISPLAY_ORDER_CODE, ST_CD) required
    ///
    /// RFC: ZECOMM_ORD_HEAD_POST | Function Group: ZPOWER_BI | SAP DEV
    /// Table written: ZECOMM_ORD_HEAD
    /// </summary>
    [RoutePrefix("api")]
    public class ZECOMM_ORD_HEAD_POSTController : BaseController
    {
        [HttpPost, Route("ZECOMM_ORD_HEAD_POST")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] EcommOrdHeadPostRequest request)
        {
            try
            {
                if (request == null || request.IT_DATA == null || request.IT_DATA.Count == 0)
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status  = "E",
                        Message = "IT_DATA array is required and must contain at least one record"
                    });

                // Validate key fields on every row before touching SAP
                for (int i = 0; i < request.IT_DATA.Count; i++)
                {
                    var row = request.IT_DATA[i];
                    if (string.IsNullOrWhiteSpace(row.SALES_ORDER_CODE) ||
                        string.IsNullOrWhiteSpace(row.DISPLAY_ORDER_CODE) ||
                        string.IsNullOrWhiteSpace(row.ST_CD))
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Status  = "E",
                            Message = $"Row {i + 1}: SALES_ORDER_CODE, DISPLAY_ORDER_CODE, ST_CD are required"
                        });
                    }
                }

                // RULE 4: SAP connector — DEV environment
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction("ZECOMM_ORD_HEAD_POST");

                // Optional test run flag
                rfcFunction.SetValue("IV_TEST_RUN",
                    request.IV_TEST_RUN ? "X" : "");

                // RULE 3: TABLE input — Append then SetValue per row
                IRfcTable itData = rfcFunction.GetTable("IT_DATA");
                foreach (var row in request.IT_DATA)
                {
                    itData.Append();
                    itData.SetValue("SALES_ORDER_CODE",    row.SALES_ORDER_CODE     ?? "");
                    itData.SetValue("DISPLAY_ORDER_CODE",  row.DISPLAY_ORDER_CODE   ?? "");
                    itData.SetValue("ST_CD",               row.ST_CD                ?? "");
                    itData.SetValue("DISPLAY_ORDER_DATE",  row.DISPLAY_ORDER_DATE   ?? "");
                    itData.SetValue("DISPLAY_ORDER_TIME",  row.DISPLAY_ORDER_TIME   ?? "");
                    itData.SetValue("CUSTOMER_CODE",       row.CUSTOMER_CODE        ?? "");
                    itData.SetValue("CUSTOMER_NAME",       row.CUSTOMER_NAME        ?? "");
                    itData.SetValue("CUSTOMER_GST_NO",     row.CUSTOMER_GST_NO      ?? "");
                    itData.SetValue("CHANNEL",             row.CHANNEL              ?? "");
                    itData.SetValue("NOTIFICATION_EMAIL",  row.NOTIFICATION_EMAIL   ?? "");
                    itData.SetValue("NOTIFICATION_MOBILE", row.NOTIFICATION_MOBILE  ?? "");
                    itData.SetValue("CASH_ON_DELIVERY",    row.CASH_ON_DELIVERY     ?? "");
                    itData.SetValue("PAYMENT_INSTRUMENT",  row.PAYMENT_INSTRUMENT   ?? "");
                    itData.SetValue("SHIPPING_CHARGES",    row.SHIPPING_CHARGES     ?? "0");
                    itData.SetValue("GIFTWRAP_CHARGES",    row.GIFTWRAP_CHARGES     ?? "0");
                    itData.SetValue("COD_CHARGES",         row.COD_CHARGES          ?? "0");
                    itData.SetValue("PICK_FROM_STORE",     row.PICK_FROM_STORE      ?? "0");
                    itData.SetValue("PREIMIMIM_DELIVERY",  row.PREIMIMIM_DELIVERY   ?? "");
                    itData.SetValue("CUSTOM_FIELD_VALUES2",row.CUSTOM_FIELD_VALUES2 ?? "");
                    itData.SetValue("CUSTOM_FIELD_NAME3",  row.CUSTOM_FIELD_NAME3   ?? "");
                    itData.SetValue("CUSTOM_FIELD_VALUES3",row.CUSTOM_FIELD_VALUES3 ?? "");
                }

                rfcFunction.Invoke(dest);

                // RULE 1: EX_RETURN is BAPIRET2 STRUCTURE → GetStructure (not GetTable)
                IRfcStructure exReturn = rfcFunction.GetStructure("EX_RETURN");
                int evCount = 0;
                try { evCount = (int)rfcFunction.GetInt("EV_COUNT"); } catch { }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status      = exReturn.GetString("TYPE"),
                    Message     = exReturn.GetString("MESSAGE"),
                    RecordCount = evCount,
                    TestRun     = request.IV_TEST_RUN,
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
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = ex.Message });
            }
        }
    }

    /// <summary>Request body for ZECOMM_ORD_HEAD_POST.</summary>
    public class EcommOrdHeadPostRequest
    {
        /// <summary>
        /// Pass true to validate records without writing to SAP (dry run).
        /// Useful for testing your payload before committing.
        /// </summary>
        public bool IV_TEST_RUN { get; set; } = false;

        /// <summary>
        /// Array of order header records to write to ZECOMM_ORD_HEAD.
        /// Key fields (required): SALES_ORDER_CODE, DISPLAY_ORDER_CODE, ST_CD.
        /// All other fields optional.
        /// </summary>
        public List<EcommOrdHeadRow> IT_DATA { get; set; }
    }

    public class EcommOrdHeadRow
    {
        /// <summary>Sales Order Code (key, CHAR45) — required</summary>
        public string SALES_ORDER_CODE    { get; set; }
        /// <summary>Display Order Code (key, CHAR45) — required</summary>
        public string DISPLAY_ORDER_CODE  { get; set; }
        /// <summary>Store Code (key, CHAR4) — required. e.g. "B03"</summary>
        public string ST_CD               { get; set; }
        /// <summary>Order date YYYYMMDD e.g. "20260504"</summary>
        public string DISPLAY_ORDER_DATE  { get; set; }
        /// <summary>Order time HHMMSS e.g. "143022"</summary>
        public string DISPLAY_ORDER_TIME  { get; set; }
        public string CUSTOMER_CODE       { get; set; }
        public string CUSTOMER_NAME       { get; set; }
        public string CUSTOMER_GST_NO     { get; set; }
        /// <summary>Channel e.g. "ONLINE", "APP"</summary>
        public string CHANNEL             { get; set; }
        public string NOTIFICATION_EMAIL  { get; set; }
        public string NOTIFICATION_MOBILE { get; set; }
        /// <summary>"YES" or "NO"</summary>
        public string CASH_ON_DELIVERY    { get; set; }
        public string PAYMENT_INSTRUMENT  { get; set; }
        public string SHIPPING_CHARGES    { get; set; }
        public string GIFTWRAP_CHARGES    { get; set; }
        public string COD_CHARGES         { get; set; }
        public string PICK_FROM_STORE     { get; set; }
        public string PREIMIMIM_DELIVERY  { get; set; }
        public string CUSTOM_FIELD_VALUES2 { get; set; }
        public string CUSTOM_FIELD_NAME3  { get; set; }
        public string CUSTOM_FIELD_VALUES3 { get; set; }
    }
}
