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
        public HttpResponseMessage GetAllowedValues(string atnam = "",
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
        // POST /api/article/patch  (single MATNR)
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

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                ArticlePatchItemResult item = InvokePatchRfc(
                    dest, request.Matnr, request.Changes, request.TestMode, request.User);
                return Request.CreateResponse(HttpStatusCode.OK, new ArticlePatchResponse
                {
                    Status = item.Ok,
                    Env = env,
                    Matnr = request.Matnr,
                    TestMode = request.TestMode,
                    ResultJson = item.ResultJson
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = false, Message = ex.Message });
            }
        }

        // ============================================================
        // POST /api/article/patch-bulk  (1..500 MATNRs in one call)
        //   body = { items:[{matnr, changes:{fn:val,...}},...],
        //            test_mode, user, stop_on_error }
        // Sequential per-MATNR RFC invoke against pooled RfcDestination.
        // Each MATNR is atomic (RFC commits per call).  No global rollback.
        // ============================================================
        [HttpPost]
        [Route("patch-bulk")]
        public HttpResponseMessage PatchBulk([FromBody] ArticleBulkPatchRequest request, string env = "dev")
        {
            if (request == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Status = false, Message = "Request body required." });

            if (request.Items == null || request.Items.Count == 0)
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Status = false, Message = "items must contain at least one element." });

            const int MAX_ITEMS = 500;
            if (request.Items.Count > MAX_ITEMS)
                return Request.CreateResponse((HttpStatusCode)413,
                    new { Status = false, Message = $"items limit is {MAX_ITEMS} per call. Got {request.Items.Count}. Split into smaller batches." });

            RfcConfigParameters rfcPar;
            HttpResponseMessage envCheck = ResolveEnv(env, out rfcPar);
            if (envCheck != null) return envCheck;

            var results = new List<ArticlePatchItemResult>(request.Items.Count);
            int applied = 0, failed = 0;

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                for (int i = 0; i < request.Items.Count; i++)
                {
                    ArticleBulkPatchItem item = request.Items[i];
                    ArticlePatchItemResult res;
                    try
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.Matnr))
                        {
                            res = new ArticlePatchItemResult
                            {
                                Index = i,
                                Matnr = item?.Matnr ?? string.Empty,
                                Ok = false,
                                ResultJson = "{\"ok\":false,\"error\":\"matnr_required\"}"
                            };
                        }
                        else if (item.Changes == null || item.Changes.Count == 0)
                        {
                            res = new ArticlePatchItemResult
                            {
                                Index = i,
                                Matnr = item.Matnr,
                                Ok = false,
                                ResultJson = "{\"ok\":false,\"error\":\"changes_empty\"}"
                            };
                        }
                        else
                        {
                            res = InvokePatchRfc(dest, item.Matnr, item.Changes, request.TestMode, request.User);
                            res.Index = i;
                        }
                    }
                    catch (Exception ex)
                    {
                        res = new ArticlePatchItemResult
                        {
                            Index = i,
                            Matnr = item?.Matnr ?? string.Empty,
                            Ok = false,
                            ResultJson = "{\"ok\":false,\"error\":\"rfc_exception\",\"msg\":\"" +
                                         (ex.Message ?? "").Replace("\"", "'") + "\"}"
                        };
                    }

                    results.Add(res);
                    if (res.Ok) applied++; else failed++;

                    if (!res.Ok && request.StopOnError) break;
                }

                return Request.CreateResponse(HttpStatusCode.OK, new ArticleBulkPatchResponse
                {
                    Status = failed == 0,
                    Env = env,
                    TestMode = request.TestMode,
                    Total = request.Items.Count,
                    Applied = applied,
                    Failed = failed,
                    Results = results
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = false, Message = ex.Message });
            }
        }

        // ----- shared single-MATNR RFC invoke -----
        // 2026-06-04: Z_ART_PATCH_RFC_V61 (FG ZARTPV61FG1, TR S4DK925666).
        // V61 enumerates all candidate classes via KSSK + INOB->CUOBJ chain
        // (covers KLART 001 + 026), per-attr routing KSML->AUSP / mirror->ZCT04.
        // BAPI_OBJCL_CHANGE always invoked with OBJECTKEY=MATNR (resolves CUOBJ
        // internally via INOB). RFC commits per call — each MATNR atomic, no bulk rollback.
        private ArticlePatchItemResult InvokePatchRfc(
            RfcDestination dest, string matnr,
            Dictionary<string, string> changes, bool testMode, string user)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in changes)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                string safeVal = (kv.Value ?? string.Empty).Replace("|", " ").Replace("=", " ");
                if (!first) sb.Append('|');
                sb.Append(kv.Key.ToUpperInvariant()).Append('=').Append(safeVal);
                first = false;
            }

            IRfcFunction fn = dest.Repository.CreateFunction("Z_ART_PATCH_RFC_V61");
            fn.SetValue("IV_MATNR", PadMatnr(matnr));
            fn.SetValue("IV_CHANGES", sb.ToString());
            fn.SetValue("IV_TEST_MODE", testMode ? "X" : " ");
            fn.SetValue("IV_USER", (user ?? "RFC_USER").ToUpperInvariant());
            fn.Invoke(dest);
            string json = fn.GetValue("EV_JSON")?.ToString() ?? "{}";
            return new ArticlePatchItemResult
            {
                Matnr = matnr,
                Ok = json.Contains("\"ok\":true"),
                ResultJson = json
            };
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

    public class ArticleBulkPatchItem
    {
        public string Matnr { get; set; }
        public Dictionary<string, string> Changes { get; set; }
    }

    public class ArticleBulkPatchRequest
    {
        /// <summary>Up to 500 MATNRs per call. Each item processed sequentially, atomic per MATNR.</summary>
        public List<ArticleBulkPatchItem> Items { get; set; }
        public bool TestMode { get; set; }
        public string User { get; set; }
        /// <summary>If true, halt loop on first failed MATNR (remaining items left unprocessed).</summary>
        public bool StopOnError { get; set; }
    }

    public class ArticlePatchItemResult
    {
        public int Index { get; set; }
        public string Matnr { get; set; }
        public bool Ok { get; set; }
        /// <summary>Raw EV_JSON from Z_ART_PATCH_RFC_V4 for this MATNR.</summary>
        public string ResultJson { get; set; }
    }

    public class ArticleBulkPatchResponse
    {
        public bool Status { get; set; }
        public string Env { get; set; }
        public bool TestMode { get; set; }
        public int Total { get; set; }
        public int Applied { get; set; }
        public int Failed { get; set; }
        public List<ArticlePatchItemResult> Results { get; set; }
    }
}
