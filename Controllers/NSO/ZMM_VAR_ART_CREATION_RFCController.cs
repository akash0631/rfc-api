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
    /// Variant Article Creation RFC.
    /// Creates variant articles in SAP MM. Accepts a list of article records via IM_DATA table.
    /// Returns EX_RETURN (ZMM_VAR_ART_MSG TABLE) with per-item creation status and messages.
    /// Note: ZMM_VAR_ART_MSG is a TABLE type (not structure) — each row is a message entry.
    /// RFC: ZMM_VAR_ART_CREATION_RFC | SAP function group: 2004_ART_CREATION_FG
    ///
    /// ROUTED TO ZMM_VAR_ART_CRT_V8 since 04-Sep-2026. The route name is kept so
    /// no caller has to change, but the RFC actually invoked is V8.
    ///
    /// Why: ZMM_VAR_ART_CREATION_RFC hands FROM_DATE / TO_DATE to
    /// ZCL_MM_ARTICLE_FINAL untouched, and the class reads them as DDMMYYYY. A
    /// caller sending the ISO YYYYMMDD that every other API here takes gets an
    /// article that is CREATED but carries NO VKP0 condition record: the VK11
    /// batch input dies on the selection screen, and the legacy FM still answers
    /// "Created Successfully" because it throws the class's PRICE_STATUS away.
    /// Proven in QA on generic 1110142997, one variable at a time:
    ///
    ///   FROM 20260904 TO 99991231  legacy -> ...035 created, NO VKP0
    ///   FROM 04092026 TO 99991231  legacy -> ...036 created, NO VKP0
    ///   FROM 04092026 TO 31129999  legacy -> ...038 created, VKP0 500.00
    ///   FROM 20260904 TO 99991231  V8     -> ...037 created, VKP0 500.00
    ///
    /// EITHER date in ISO is enough to lose the price. In PROD that had left 5,598
    /// variants across 679 generics unpriced between 24-Aug and 04-Sep, which MDM
    /// was keying by hand.
    ///
    /// V8 normalises both notations, defaults blanks, refuses a blank rate (which
    /// VK11 would otherwise accept and book as 0.00), is idempotent on
    /// (generic, size, colour), and verifies the price against A073 + KONP instead
    /// of trusting a batch-input message.
    ///
    /// The legacy FM is deliberately NOT patched: it lives in the multi-FM group
    /// ZMM_ART_CREATION_FG, where a headless patch on 02-Jun-2026 left an orphan
    /// FUNCTION declaration in the old U-include and broke every sibling FM in the
    /// pool. V8 is already in PROD in its own group, so routing costs nothing and
    /// risks nothing.
    /// </summary>
    [RoutePrefix("api")]
    public class ZMM_VAR_ART_CREATION_RFCController : BaseController
    {
        /// <summary>The RFC this route actually invokes. See the class remarks.</summary>
        private const string RoutedRfc = "ZMM_VAR_ART_CRT_V8";

        [HttpPost, Route("ZMM_VAR_ART_CREATION_RFC")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] VarArtCreationRequest request)
        {
            try
            {
                if (request == null) request = new VarArtCreationRequest();

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction(RoutedRfc);

                // Table: IM_DATA (ZMM_VAR_ART_TT) — variant article records — RULE 3
                if (request.IM_DATA != null && request.IM_DATA.Count > 0)
                {
                    IRfcTable imData = rfcFunction.GetTable("IM_DATA");
                    foreach (var item in request.IM_DATA)
                    {
                        imData.Append();
                        imData.SetValue("GENERIC_ARTICLE", item.GENERIC_ARTICLE  ?? "");
                        imData.SetValue("VARIANT_ARTICLE", item.VARIANT_ARTICLE  ?? "");
                        imData.SetValue("VAR1CHAR1",       item.VAR1CHAR1        ?? "");
                        imData.SetValue("VAR1VAL1",        item.VAR1VAL1         ?? "");
                        imData.SetValue("VAR1CHAR2",       item.VAR1CHAR2        ?? "");
                        imData.SetValue("VAR1VAL2",        item.VAR1VAL2         ?? "");
                        imData.SetValue("VENDOR",          item.VENDOR           ?? "");
                        imData.SetValue("SITE",            item.SITE             ?? "");
                        imData.SetValue("PUR_GRP",         item.PUR_GRP          ?? "");
                        imData.SetValue("NET_PRICE",       item.NET_PRICE        ?? "");
                        imData.SetValue("SALES_ORG",       item.SALES_ORG        ?? "");
                        imData.SetValue("SALES_UNIT",      item.SALES_UNIT       ?? "");
                        imData.SetValue("MRP_TYPE",        item.MRP_TYPE         ?? "");
                        imData.SetValue("FROM_DATE",       item.FROM_DATE        ?? "");
                        imData.SetValue("TO_DATE",         item.TO_DATE          ?? "");
                        imData.SetValue("OLD_MAT_NO",      item.OLD_MAT_NO       ?? "");
                        imData.SetValue("TAX_CODE",        item.TAX_CODE         ?? "");
                    }
                }

                rfcFunction.Invoke(dest);

                // FIX: EX_RETURN is ZMM_VAR_ART_MSG which is a TABLE TYPE (not a structure).
                // Each row in the table is a message entry with TYPE and MESSAGE fields.
                IRfcTable exReturnTable = rfcFunction.GetTable("EX_RETURN");
                var messages = new List<object>();
                string overallStatus = "S";
                string overallMessage = "Completed";

                foreach (IRfcStructure row in exReturnTable)
                {
                    string rowType = row.GetString("TYPE");
                    string rowMsg  = row.GetString("MESSAGE");

                    // V8 emits a SECOND row per input carrying the price verdict,
                    // which the legacy FM never returned. Report a price failure as
                    // a warning, not an error: the article IS created, and a caller
                    // that reads Status "E" as "creation failed" would otherwise
                    // start retrying articles that exist. Creation still decides
                    // Status, exactly as it did before this route moved to V8.
                    bool isPriceRow = rowMsg != null
                        && rowMsg.TrimStart().StartsWith("VKP0", StringComparison.Ordinal);
                    if (isPriceRow && (rowType == "E" || rowType == "A")) rowType = "W";

                    messages.Add(new
                    {
                        TYPE    = rowType,
                        ID      = row.GetString("ID"),
                        NUMBER  = row.GetString("NUMBER"),
                        MESSAGE = rowMsg
                    });
                    // If any row is error, overall status is error
                    if (rowType == "E" || rowType == "A")
                    {
                        overallStatus  = "E";
                        overallMessage = rowMsg;
                    }
                }

                // If no messages at all, treat as success
                if (messages.Count == 0)
                {
                    overallStatus  = "S";
                    overallMessage = "No messages returned — check SAP for result";
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status  = overallStatus,
                    Message = overallMessage,
                    // Named so nobody debugging this has to guess which FM ran.
                    RfcRouted = "ZMM_VAR_ART_CREATION_RFC -> " + RoutedRfc,
                    Data = new { EX_RETURN = messages }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class VarArtCreationRequest { public List<VarArtItem> IM_DATA { get; set; } }
    public class VarArtItem
    {
        public string GENERIC_ARTICLE { get; set; }  public string VARIANT_ARTICLE { get; set; }
        public string VAR1CHAR1       { get; set; }  public string VAR1VAL1        { get; set; }
        public string VAR1CHAR2       { get; set; }  public string VAR1VAL2        { get; set; }
        public string VENDOR          { get; set; }  public string SITE            { get; set; }
        public string PUR_GRP         { get; set; }  public string NET_PRICE       { get; set; }
        public string SALES_ORG       { get; set; }  public string SALES_UNIT      { get; set; }
        public string MRP_TYPE        { get; set; }  public string FROM_DATE       { get; set; }
        public string TO_DATE         { get; set; }  public string OLD_MAT_NO      { get; set; }
        public string TAX_CODE        { get; set; }
    }
}
