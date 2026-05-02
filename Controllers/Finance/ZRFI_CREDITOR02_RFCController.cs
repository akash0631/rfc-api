using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.Finance
{
    /// <summary>
    /// Creditor Report RFC.
    /// Returns creditor/vendor payment data including ageing buckets, due dates, document details.
    /// Import: IM_DATE (system date for report). Export: ET_DATA (ZEI_CREDITOR01_ITR), EX_RETURN (BAPIRET2).
    /// RFC: ZRFI_CREDITOR02_RFC | Function group: ZFI_CREDITOR01
    /// </summary>
    [RoutePrefix("api")]
    public class ZRFI_CREDITOR02_RFCController : BaseController
    {
        [HttpPost, Route("ZRFI_CREDITOR02_RFC")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] CreditorReportRequest request)
        {
            try
            {
                if (request == null) request = new CreditorReportRequest();

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction("ZRFI_CREDITOR02_RFC");

                // Import: IM_DATE — defaults to today if not provided (SY-DATUM)
                string reportDate = !string.IsNullOrEmpty(request.IM_DATE)
                    ? request.IM_DATE
                    : DateTime.Today.ToString("yyyyMMdd");
                rfcFunction.SetValue("IM_DATE", reportDate);

                rfcFunction.Invoke(dest);

                // RULE 1: EX_RETURN is BAPIRET2 STRUCTURE → GetStructure (not GetTable)
                IRfcStructure exReturn = rfcFunction.GetStructure("EX_RETURN");

                // Output table: ET_DATA (ZEI_CREDITOR01_ITR line type ZEI_CREDITOR01_RFC)
                IRfcTable etData = rfcFunction.GetTable("ET_DATA");
                var rows = new List<object>();
                foreach (IRfcStructure row in etData)
                {
                    rows.Add(new
                    {
                        BUKRS        = row.GetString("BUKRS"),
                        BELNR        = row.GetString("BELNR"),
                        BLART        = row.GetString("BLART"),
                        GJAHR        = row.GetString("GJAHR"),
                        GSBER        = row.GetString("GSBER"),
                        LIFNR        = row.GetString("LIFNR"),
                        AKONT        = row.GetString("AKONT"),
                        NAME1        = row.GetString("NAME1"),
                        ORT01        = row.GetString("ORT01"),
                        BUDAT        = row.GetString("BUDAT"),
                        BLDAT        = row.GetString("BLDAT"),
                        DMBTR_C      = row.GetString("DMBTR_C"),
                        DMBTR_D      = row.GetString("DMBTR_D"),
                        AMOUNT_DESV  = row.GetString("AMOUNT_DESV"),
                        ADV_AMT      = row.GetString("ADV_AMT"),
                        R030         = row.GetString("R030"),
                        R060         = row.GetString("R060"),
                        R120         = row.GetString("R120"),
                        R180         = row.GetString("R180"),
                        R365         = row.GetString("R365"),
                        R730         = row.GetString("R730"),
                        RGT731       = row.GetString("RGT731"),
                        CF_AMT       = row.GetString("CF_AMT"),
                        PI_AMT       = row.GetString("PI_AMT"),
                        ZTERM        = row.GetString("ZTERM"),
                        TERM_TEXT    = row.GetString("TERM_TEXT"),
                        DUE_DATE     = row.GetString("DUE_DATE"),
                        DUE_DAY      = row.GetString("DUE_DAY"),
                        AGE_GRP      = row.GetString("AGE_GRP"),
                        SGTXT        = row.GetString("SGTXT"),
                        EBELN        = row.GetString("EBELN"),
                        INVNO        = row.GetString("INVNO"),
                        INVDT        = row.GetString("INVDT"),
                        INV_QTY      = row.GetString("INV_QTY"),
                        INV_VAL      = row.GetString("INV_VAL")
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status      = exReturn.GetString("TYPE"),
                    Message     = exReturn.GetString("MESSAGE"),
                    RecordCount = rows.Count,
                    ReportDate  = reportDate,
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

    public class CreditorReportRequest
    {
        /// <summary>Report date in YYYYMMDD format. Defaults to today (SY-DATUM).</summary>
        public string IM_DATE { get; set; }
    }
}
