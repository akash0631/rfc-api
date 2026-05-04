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
    /// eCommerce Order Header RFC.
    /// Reads order header records from ZECOMM_ORD_HEAD (eCommerce orders table).
    /// Supports flexible filtering: by store, date range, channel, or specific order codes.
    ///
    /// Filter priority (first non-empty wins):
    ///   1. IV_SALES_ORDER_CODE   — exact order lookup
    ///   2. IV_DISPLAY_ORDER_CODE — exact display order lookup
    ///   3. IV_ST_CD + date range + channel (any combination)
    ///
    /// Returns all 22 header fields: order codes, customer details, channel,
    /// payment info, charges (shipping/giftwrap/COD/pick), custom fields.
    ///
    /// RFC: ZECOMM_ORD_HEAD_RFC | Function Group: ZPOWER_BI | SAP DEV only
    /// Table: ZECOMM_ORD_HEAD | Keys: MANDT + SALES_ORDER_CODE + DISPLAY_ORDER_CODE + ST_CD
    /// </summary>
    [RoutePrefix("api")]
    public class ZECOMM_ORD_HEAD_RFCController : BaseController
    {
        [HttpPost, Route("ZECOMM_ORD_HEAD_RFC")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] EcommOrdHeadRequest request)
        {
            try
            {
                if (request == null) request = new EcommOrdHeadRequest();

                // RULE 4: SAP connector pattern — DEV (function exists in ZPOWER_BI on DEV)
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction("ZECOMM_ORD_HEAD_RFC");

                // Import scalar parameters — all optional
                if (!string.IsNullOrEmpty(request.IV_ST_CD))
                    rfcFunction.SetValue("IV_ST_CD", request.IV_ST_CD);

                if (!string.IsNullOrEmpty(request.IV_SALES_ORDER_CODE))
                    rfcFunction.SetValue("IV_SALES_ORDER_CODE", request.IV_SALES_ORDER_CODE);

                if (!string.IsNullOrEmpty(request.IV_DISPLAY_ORDER_CODE))
                    rfcFunction.SetValue("IV_DISPLAY_ORDER_CODE", request.IV_DISPLAY_ORDER_CODE);

                if (!string.IsNullOrEmpty(request.IV_CHANNEL))
                    rfcFunction.SetValue("IV_CHANNEL", request.IV_CHANNEL);

                if (!string.IsNullOrEmpty(request.IV_FROM_DATE))
                    rfcFunction.SetValue("IV_FROM_DATE", request.IV_FROM_DATE);

                if (!string.IsNullOrEmpty(request.IV_TO_DATE))
                    rfcFunction.SetValue("IV_TO_DATE", request.IV_TO_DATE);

                if (request.IV_MAX_ROWS > 0)
                    rfcFunction.SetValue("IV_MAX_ROWS", request.IV_MAX_ROWS);

                rfcFunction.Invoke(dest);

                // RULE 1: EX_RETURN is BAPIRET2 STRUCTURE → GetStructure (not GetTable)
                IRfcStructure exReturn = rfcFunction.GetStructure("EX_RETURN");
                int evCount = 0;
                try { evCount = (int)rfcFunction.GetInt("EV_COUNT"); } catch { }

                // RULE 13: TABLE output → GetTable + iterate
                IRfcTable etData = rfcFunction.GetTable("ET_DATA");
                var rows = new List<object>();
                foreach (IRfcStructure row in etData)
                {
                    rows.Add(new
                    {
                        SALES_ORDER_CODE      = row.GetString("SALES_ORDER_CODE"),
                        DISPLAY_ORDER_CODE    = row.GetString("DISPLAY_ORDER_CODE"),
                        ST_CD                 = row.GetString("ST_CD"),
                        DISPLAY_ORDER_DATE    = row.GetString("DISPLAY_ORDER_DATE"),
                        DISPLAY_ORDER_TIME    = row.GetString("DISPLAY_ORDER_TIME"),
                        CUSTOMER_CODE         = row.GetString("CUSTOMER_CODE"),
                        CUSTOMER_NAME         = row.GetString("CUSTOMER_NAME"),
                        CUSTOMER_GST_NO       = row.GetString("CUSTOMER_GST_NO"),
                        CHANNEL               = row.GetString("CHANNEL"),
                        NOTIFICATION_EMAIL    = row.GetString("NOTIFICATION_EMAIL"),
                        NOTIFICATION_MOBILE   = row.GetString("NOTIFICATION_MOBILE"),
                        CASH_ON_DELIVERY      = row.GetString("CASH_ON_DELIVERY"),
                        PAYMENT_INSTRUMENT    = row.GetString("PAYMENT_INSTRUMENT"),
                        SHIPPING_CHARGES      = row.GetString("SHIPPING_CHARGES"),
                        GIFTWRAP_CHARGES      = row.GetString("GIFTWRAP_CHARGES"),
                        COD_CHARGES           = row.GetString("COD_CHARGES"),
                        PICK_FROM_STORE       = row.GetString("PICK_FROM_STORE"),
                        PREIMIMIM_DELIVERY    = row.GetString("PREIMIMIM_DELIVERY"),
                        CUSTOM_FIELD_VALUES2  = row.GetString("CUSTOM_FIELD_VALUES2"),
                        CUSTOM_FIELD_NAME3    = row.GetString("CUSTOM_FIELD_NAME3"),
                        CUSTOM_FIELD_VALUES3  = row.GetString("CUSTOM_FIELD_VALUES3")
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status      = exReturn.GetString("TYPE"),
                    Message     = exReturn.GetString("MESSAGE"),
                    RecordCount = evCount,
                    Filters = new
                    {
                        StoreCode         = request.IV_ST_CD,
                        SalesOrderCode    = request.IV_SALES_ORDER_CODE,
                        DisplayOrderCode  = request.IV_DISPLAY_ORDER_CODE,
                        Channel           = request.IV_CHANNEL,
                        FromDate          = request.IV_FROM_DATE,
                        ToDate            = request.IV_TO_DATE,
                        MaxRows           = request.IV_MAX_ROWS
                    },
                    Data = new { ET_DATA = rows }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = ex.Message });
            }
        }
    }

    /// <summary>Request model for ZECOMM_ORD_HEAD_RFC — all parameters optional.</summary>
    public class EcommOrdHeadRequest
    {
        /// <summary>Store code (ZST_CD, 4 chars) e.g. "B03"</summary>
        public string IV_ST_CD { get; set; }

        /// <summary>Exact Sales Order Code (CHAR45) — highest priority filter</summary>
        public string IV_SALES_ORDER_CODE { get; set; }

        /// <summary>Exact Display Order Code (CHAR45) — second priority filter</summary>
        public string IV_DISPLAY_ORDER_CODE { get; set; }

        /// <summary>Channel filter (CHAR6) e.g. "ONLINE", "APP"</summary>
        public string IV_CHANNEL { get; set; }

        /// <summary>From date (YYYYMMDD) e.g. "20260101"</summary>
        public string IV_FROM_DATE { get; set; }

        /// <summary>To date (YYYYMMDD) e.g. "20260504"</summary>
        public string IV_TO_DATE { get; set; }

        /// <summary>Max rows to return (default 1000, max 5000)</summary>
        public int IV_MAX_ROWS { get; set; } = 1000;
    }
}
