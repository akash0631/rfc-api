using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.MM
{
    /// <summary>
    /// Article Characteristic PATCH/READ/SCHEMA API.
    ///
    /// SAP FG ZART_CHAR_PATCH2 (TR S4DK925570).
    /// FMs:
    ///   Z_ART_SCHEMA_RFC  -> EV_JSON = {"locked":["MATNR","COLOR",...16 names]}
    ///   Z_ART_READ_RFC    -> EV_JSON = {"matnr":"X","fields":[{"fn":"X","value":"Y"},...]}
    ///   Z_ART_PATCH_RFC   -> EV_JSON = {"matnr":"X","ok":true|false,"applied":N,"errors":[...]}
    ///
    /// Endpoints:
    ///   GET  /api/article/char-schema?env=dev|qa
    ///   GET  /api/article/{matnr}?env=dev|qa
    ///   POST /api/article/patch?env=dev|qa  body={matnr, changes:{fn:value,...}, test_mode:bool, user:"AKASH"}
    ///
    /// PROD = HARD 403 until business sign-off.
    /// Lock list enforced server-side in ABAP — client cannot bypass.
    /// </summary>
    [RoutePrefix("api/article")]
    public class ArticleCharController : BaseController
    {
        // ============================================================
        // GET /api/article/char-schema
        // ============================================================
        [HttpGet]
        [Route("char-schema")]
        public HttpResponseMessage GetSchema(string env = "dev")
        {
            RfcConfigParameters rfcPar;
            HttpResponseMessage envCheck = ResolveEnv(env, out rfcPar);
            if (envCheck != null) return envCheck;

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_ART_SCHEMA_RFC");
                fn.SetValue("IV_DUMMY", "X");
                fn.Invoke(dest);
                string json = fn.GetValue("EV_JSON")?.ToString() ?? "{}";
                return Request.CreateResponse(HttpStatusCode.OK, new ArticleSchemaResponse
                {
                    Status = true,
                    Env = env,
                    SchemaJson = json
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = false, Message = ex.Message });
            }
        }

        // ============================================================
        // GET /api/article/allowed-values?atnam=F_WIDTH&matnr=<optional>
        // Returns CAWN allowed value list for a characteristic.
        // Backed by Z_ART_ALLOWED_VALS_RFC (FG ZARTALLOWED, TR S4DK925628).
        // source field on response tells frontend whether values apply to
        // this MATNR's class (CABN_IN_CLASS / CABN_GLOBAL_NOT_IN_CLASS /
        // CABN_GLOBAL). Use to render dropdown UI in article editor.
        // ============================================================
        [HttpGet]
        [Route("allowed-values")]
        public HttpResponseMessage GetAllowedValues(string atnam,
            string matnr = "", string env = "dev")
        {
            if (string.IsNullOrWhiteSpace(atnam))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Status = false, Message = "atnam query param required." });

            RfcConfigParameters rfcPar;
            HttpResponseMessage envCheck = ResolveEnv(env, out rfcPar);
            if (envCheck != null) return envCheck;

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_ART_ALLOWED_VALS_RFC");
                fn.SetValue("IV_ATNAM", atnam.ToUpperInvariant());
                fn.SetValue("IV_MATNR",
                    string.IsNullOrWhiteSpace(matnr) ? "" : PadMatnr(matnr));
                fn.Invoke(dest);
                string json = fn.GetValue("EV_JSON")?.ToString() ?? "{}";
                return Request.CreateResponse(HttpStatusCode.OK, new ArticleAllowedValuesResponse
                {
                    Status = json.Contains("\"ok\":true"),
                    Env = env,
                    Matnr = matnr ?? "",
                    Atnam = atnam,
                    AllowedJson = json
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = false, Message = ex.Message });
            }
        }

        // ============================================================
        // GET /api/article/{matnr}
        // ============================================================
        [HttpGet]
        [Route("{matnr}")]
        public HttpResponseMessage GetArticle(string matnr, string env = "dev")
        {
            if (string.IsNullOrWhiteSpace(matnr))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Status = false, Message = "matnr required." });

            RfcConfigParameters rfcPar;
            HttpResponseMessage envCheck = ResolveEnv(env, out rfcPar);
            if (envCheck != null) return envCheck;

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_ART_READ_RFC");
                fn.SetValue("IV_MATNR", PadMatnr(matnr));
                fn.Invoke(dest);
                string json = fn.GetValue("EV_JSON")?.ToString() ?? "{}";
                return Request.CreateResponse(HttpStatusCode.OK, new ArticleReadResponse
                {
                    Status = true,
                    Env = env,
                    Matnr = matnr,
                    ArticleJson = json
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = false, Message = ex.Message });
            }
        }

        // ============================================================
        // POST /api/article/patch
        // ============================================================
        [HttpPost]
        [Route("patch")]
        public HttpResponseMessage Patch([FromBody] ArticlePatchRequest request, string env = "dev")
        {
            if (request == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Status = false, Message = "Request body required." });

            if (string.IsNullOrWhiteSpace(request.Matnr))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Status = false, Message = "matnr required." });

            if (request.Changes == null || request.Changes.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Status = false, Message = "changes must contain at least one fieldname=value pair." });

            RfcConfigParameters rfcPar;
            HttpResponseMessage envCheck = ResolveEnv(env, out rfcPar);
            if (envCheck != null) return envCheck;

            // Build pipe-delimited string M_FIT=SLIM|M_PATTERN=SOLID
            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in request.Changes)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                // Defensive: strip pipe/equals from values to avoid parser injection
                string safeVal = (kv.Value ?? string.Empty).Replace("|", " ").Replace("=", " ");
                if (!first) sb.Append('|');
                sb.Append(kv.Key.ToUpperInvariant()).Append('=').Append(safeVal);
                first = false;
            }
            string changesStr = sb.ToString();

            try
            {
                // 2026-06-02: flipped to Z_ART_PATCH_RFC_V4 (FG ZARTPV4FG, TR S4DK925598).
                // V4 fixes the V3 routing bug where classified MATNRs could not update
                // ZCT04-mirror-only fields. V3 routed by has_class(matnr): any attr found
                // in CABN globally was sent to BAPI_OBJCL_CHANGE, which rejected attrs not
                // in that class's KSML (e.g. M_FIT on a class-774 MATNR).
                // V4 routes per attr using all three signals:
                //   - attr ATINN in this MATNR's class KSML  -> AUSP (BAPI route)
                //   - else attr is a ZCT04_CHARACTER column   -> ZCT04 mirror (direct MODIFY)
                //   - else attr in CABN globally but not class -> NOT_IN_CLASS error
                //   - else                                     -> UNKNOWN error
                // Response now includes per-attr "results"/"plan" array with route + status
                // so callers can show field-level success/failure detail (master-data UX).
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_ART_PATCH_RFC_V4");
                fn.SetValue("IV_MATNR", PadMatnr(request.Matnr));
                fn.SetValue("IV_CHANGES", changesStr);
                fn.SetValue("IV_TEST_MODE", request.TestMode ? "X" : " ");
                fn.SetValue("IV_USER", (request.User ?? "RFC_USER").ToUpperInvariant());
                fn.Invoke(dest);
                string json = fn.GetValue("EV_JSON")?.ToString() ?? "{}";
                bool ok = json.Contains("\"ok\":true");
                return Request.CreateResponse(HttpStatusCode.OK, new ArticlePatchResponse
                {
                    Status = ok,
                    Env = env,
                    Matnr = request.Matnr,
                    TestMode = request.TestMode,
                    ResultJson = json
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = false, Message = ex.Message });
            }
        }

        // ============================================================
        // helpers
        // ============================================================
        private HttpResponseMessage ResolveEnv(string env, out RfcConfigParameters rfcPar)
        {
            rfcPar = null;
            string envNorm = (env ?? "dev").Trim().ToLowerInvariant();
            switch (envNorm)
            {
                case "qa":
                case "quality":
                    rfcPar = BaseController.rfcConfigparametersquality();
                    return null;
                case "prod":
                case "production":
                    return Request.CreateResponse(HttpStatusCode.Forbidden,
                        new { Status = false, Message = "env=prod is hard-blocked on Article PATCH API. Use dev or qa." });
                case "dev":
                case "development":
                case "":
                    rfcPar = BaseController.rfcConfigparameters();
                    return null;
                default:
                    return Request.CreateResponse(HttpStatusCode.BadRequest,
                        new { Status = false, Message = $"Invalid env '{env}'. Use dev | qa." });
            }
        }

        /// <summary>SAP MATNR is CHAR40 with leading zero pad up to 18 chars per V2 convention.</summary>
        private static string PadMatnr(string matnr)
        {
            if (string.IsNullOrWhiteSpace(matnr)) return string.Empty;
            string trimmed = matnr.Trim();
            if (trimmed.Length >= 18) return trimmed;
            // Numeric-only MATNRs get left-padded with zeros to 18 chars
            return long.TryParse(trimmed, out long _) ? trimmed.PadLeft(18, '0') : trimmed;
        }
    }

    public class ArticlePatchRequest
    {
        /// <summary>SAP article number (numeric MATNR, will be left-padded to 18).</summary>
        public string Matnr { get; set; }

        /// <summary>Sparse field=value map. Server rejects locked fields with BAPIRET2 'E'.</summary>
        public Dictionary<string, string> Changes { get; set; }

        /// <summary>If true, validate only — no commit. Returns proposed change count.</summary>
        public bool TestMode { get; set; }

        /// <summary>App user identity for SLG1 audit. Defaults to RFC_USER.</summary>
        public string User { get; set; }
    }

    public class ArticleSchemaResponse
    {
        public bool Status { get; set; }
        public string Env { get; set; }
        /// <summary>Raw EV_JSON: {"locked":[...16 field names...]}.</summary>
        public string SchemaJson { get; set; }
    }

    public class ArticleReadResponse
    {
        public bool Status { get; set; }
        public string Env { get; set; }
        public string Matnr { get; set; }
        /// <summary>Raw EV_JSON: {"matnr":"X","fields":[{"fn":"M_FIT","value":"SLIM"},...]} or {"found":false} if absent.</summary>
        public string ArticleJson { get; set; }
    }

    public class ArticleAllowedValuesResponse
    {
        public bool Status { get; set; }
        public string Env { get; set; }
        public string Matnr { get; set; }
        public string Atnam { get; set; }
        /// <summary>Raw EV_JSON: {"ok":true,"atnam":"F_WIDTH","atinn":"0000000979","source":"CABN_IN_CLASS","allowed":["20","30",...]}</summary>
        public string AllowedJson { get; set; }
    }

    public class ArticlePatchResponse
    {
        public bool Status { get; set; }
        public string Env { get; set; }
        public string Matnr { get; set; }
        public bool TestMode { get; set; }
        /// <summary>Raw EV_JSON: {"matnr":"X","ok":true,"applied":N} or {"ok":false,"errors":[{"fn":"X","msg":"LOCKED"}]}.</summary>
        public string ResultJson { get; set; }
    }
}
