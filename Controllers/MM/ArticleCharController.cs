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
    ///   GET  /api/article/{matnr}?env=dev|qa|prod
    ///   POST /api/article/patch?env=dev|qa|prod  body={matnr, changes:{fn:value,...}, test_mode:bool, user:"AKASH"}
    ///
    /// PROD live 2026-06-12 (Z_ART_PATCH_RFC_V61 imported on S4P; business sign-off received).
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

        // ============================================================
        // POST /api/article/patch-v65-chain
        //   body = { items:[{matnr, attrs:[{atnam,atwrt},...]},...], user }
        // Runs the full 3-FM chain PER MATNR against V65:
        //   1) RFC_READ_TABLE(MARA) → resolve MATKL
        //   2) Z_LINK_MATNR_CLASS → ensure article linked to class (KLART=026)
        //   3) Z_ART_PATCH_RFC_V65 → write AUSP + ZCT04 mirror
        // Prevents the silent NIC no-op that hits Pool B when it calls V65 via
        // /api/rfc/proxy without the pre-link step.
        // Sequential per MATNR. Each MATNR atomic (RFC auto-commits).
        // ============================================================
        [HttpPost]
        [Route("patch-v65-chain")]
        public HttpResponseMessage PatchV65Chain([FromBody] ArticleV65ChainRequest request, string env = "dev")
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
                    new { Status = false, Message = $"items limit is {MAX_ITEMS} per call. Got {request.Items.Count}." });

            RfcConfigParameters rfcPar;
            HttpResponseMessage envCheck = ResolveEnv(env, out rfcPar);
            if (envCheck != null) return envCheck;

            var results = new List<ArticleV65ChainItemResult>(request.Items.Count);
            int applied = 0, failed = 0;

            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                for (int i = 0; i < request.Items.Count; i++)
                {
                    ArticleV65ChainItem item = request.Items[i];
                    ArticleV65ChainItemResult res = new ArticleV65ChainItemResult { Index = i };

                    try
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.Matnr))
                        {
                            res.Matnr = item?.Matnr ?? "";
                            res.Ok = false;
                            res.Error = "matnr_required";
                        }
                        else if (item.Attrs == null || item.Attrs.Count == 0)
                        {
                            res.Matnr = item.Matnr;
                            res.Ok = false;
                            res.Error = "attrs_empty";
                        }
                        else
                        {
                            string padded = PadMatnr(item.Matnr);
                            res.Matnr = item.Matnr;

                            // Step 1: MARA lookup → MATKL
                            string matkl = LookupMatkl(dest, padded);
                            res.Matkl = matkl;
                            if (string.IsNullOrWhiteSpace(matkl))
                            {
                                res.Ok = false;
                                res.Error = "matkl_not_found";
                            }
                            else
                            {
                                // Step 2: link MATNR ↔ class (KLART=026, IV_CLASS=MATKL).
                                // BEST-EFFORT — "Assignment exists and is valid" is a benign
                                // idempotent duplicate check, NOT a real failure. V65 also
                                // auto-enumerates classes internally, so even a hard link
                                // exception rarely blocks the patch. Log outcome but always
                                // proceed to Step 3.
                                string linkJson;
                                bool linkOk = InvokeLinkMatnrClass(dest, padded, matkl, out linkJson);
                                res.LinkOk = linkOk;
                                res.LinkMsg = linkJson;

                                // Step 3: V65 patch (runs regardless of link result)
                                string patchJson;
                                int appliedCount, nicCount;
                                bool patchOk = InvokeV65Patch(dest, padded, item.Attrs,
                                    request.User, out patchJson, out appliedCount, out nicCount);
                                res.PatchJson = patchJson;
                                res.Applied = appliedCount;
                                res.Nic = nicCount;
                                res.Ok = patchOk && appliedCount > 0;
                                if (!res.Ok)
                                {
                                    if (nicCount > 0) res.Error = "all_nic";
                                    else if (appliedCount == 0) res.Error = "no_writes";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        res.Ok = false;
                        res.Error = "rfc_exception";
                        res.ErrorMsg = ex.Message;
                    }

                    results.Add(res);
                    if (res.Ok) applied++; else failed++;
                    if (!res.Ok && request.StopOnError) break;
                }

                return Request.CreateResponse(HttpStatusCode.OK, new ArticleV65ChainResponse
                {
                    Status = failed == 0,
                    Env = env,
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

        // Step 1 helper — MARA MATKL lookup via RFC_READ_TABLE.
        // Uses single OPTIONS row; MATNR EQ '...' fits within the 72-char row limit
        // per [[reference_rfc_read_table_options_72char]].
        private string LookupMatkl(RfcDestination dest, string paddedMatnr)
        {
            IRfcFunction fn = dest.Repository.CreateFunction("RFC_READ_TABLE");
            fn.SetValue("QUERY_TABLE", "MARA");
            fn.SetValue("DELIMITER", "|");

            IRfcTable opts = fn.GetTable("OPTIONS");
            IRfcStructure optRow = opts.Metadata.LineType.CreateStructure();
            optRow.SetValue("TEXT", "MATNR EQ '" + paddedMatnr + "'");
            opts.Append(optRow);

            IRfcTable flds = fn.GetTable("FIELDS");
            IRfcStructure fldRow = flds.Metadata.LineType.CreateStructure();
            fldRow.SetValue("FIELDNAME", "MATKL");
            flds.Append(fldRow);

            fn.Invoke(dest);
            IRfcTable data = fn.GetTable("DATA");
            if (data.RowCount == 0) return string.Empty;
            data.CurrentIndex = 0;
            return (data.CurrentRow.GetString("WA") ?? "").Trim();
        }

        // Step 2 helper — link article to its class.
        // Real signature (verified 2026-07-03 via abap_read_interface):
        //   IV_MATNR (STRING), IV_CLASS (STRING), IV_KLART (STRING), EV_JSON (STRING)
        // V2 convention: IV_CLASS = MATKL, IV_KLART = '026' (batch/article class type).
        private bool InvokeLinkMatnrClass(RfcDestination dest,
            string paddedMatnr, string matkl, out string linkJson)
        {
            linkJson = "";
            try
            {
                IRfcFunction fn = dest.Repository.CreateFunction("Z_LINK_MATNR_CLASS");
                fn.SetValue("IV_MATNR", paddedMatnr);
                fn.SetValue("IV_CLASS", matkl);
                fn.SetValue("IV_KLART", "026");
                fn.Invoke(dest);
                try { linkJson = fn.GetValue("EV_JSON")?.ToString() ?? ""; } catch { }
                // Convention: EV_JSON contains "ok":true on success. Fallback = non-throw.
                if (!string.IsNullOrEmpty(linkJson) && linkJson.Contains("\"ok\":false"))
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                linkJson = "{\"ok\":false,\"error\":\"" + (ex.Message ?? "").Replace("\"", "'") + "\"}";
                return false;
            }
        }

        // Step 3 helper — V65 patch.
        // Real signature (verified 2026-07-03 via abap_read_interface):
        //   IV_MATNR (MATNR), IV_CHANGES (STRING, pipe "K=V|K=V"), IV_TEST_MODE (FLAG), EV_JSON (STRING)
        // NO IT_ATTRS table, NO IV_USER. Same shape as V61. Parses EV_JSON for
        // "applied":N + "nic":N (matches Z_ART_PATCH_RFC family convention).
        private bool InvokeV65Patch(RfcDestination dest, string paddedMatnr,
            List<ArticleV65Attr> attrs, string user,
            out string json, out int appliedCount, out int nicCount)
        {
            json = "";
            appliedCount = 0;
            nicCount = 0;

            // Build pipe-delimited K=V|K=V string. Sanitize | and = from values.
            var sb = new StringBuilder();
            bool first = true;
            foreach (var a in attrs)
            {
                if (a == null || string.IsNullOrEmpty(a.Atnam)) continue;
                string safeVal = (a.Atwrt ?? "").Replace("|", " ").Replace("=", " ");
                if (!first) sb.Append('|');
                sb.Append(a.Atnam.ToUpperInvariant()).Append('=').Append(safeVal);
                first = false;
            }

            IRfcFunction fn = dest.Repository.CreateFunction("Z_ART_PATCH_RFC_V65");
            fn.SetValue("IV_MATNR", paddedMatnr);
            fn.SetValue("IV_CHANGES", sb.ToString());
            fn.SetValue("IV_TEST_MODE", " ");
            fn.Invoke(dest);

            try { json = fn.GetValue("EV_JSON")?.ToString() ?? ""; } catch { }

            // Parse counts from EV_JSON. Minimal regex — avoids full JSON dep.
            appliedCount = ExtractIntFromJson(json, "applied");
            nicCount = ExtractIntFromJson(json, "nic");

            // Success = ok:true in EV_JSON (matches Z_ART_PATCH_RFC family contract).
            return json.Contains("\"ok\":true");
        }

        // Minimal int-from-JSON extractor. Not a full parser — just handles
        // "key":123 shape. Returns 0 on miss (safer than throwing).
        private static int ExtractIntFromJson(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            string needle = "\"" + key + "\":";
            int i = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return 0;
            i += needle.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-')) i++;
            if (i == start) return 0;
            int val;
            return int.TryParse(json.Substring(start, i - start), out val) ? val : 0;
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
                    rfcPar = BaseController.rfcConfigparametersproduction();
                    return null;
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

    // ─── V65 chain DTOs ──────────────────────────────────────────────
    public class ArticleV65Attr
    {
        public string Atnam { get; set; }
        public string Atwrt { get; set; }
    }

    public class ArticleV65ChainItem
    {
        public string Matnr { get; set; }
        public List<ArticleV65Attr> Attrs { get; set; }
    }

    public class ArticleV65ChainRequest
    {
        public List<ArticleV65ChainItem> Items { get; set; }
        public string User { get; set; }
        public bool StopOnError { get; set; }
    }

    public class ArticleV65ChainItemResult
    {
        public int Index { get; set; }
        public string Matnr { get; set; }
        public string Matkl { get; set; }
        public bool LinkOk { get; set; }
        public string LinkMsg { get; set; }
        public int Applied { get; set; }
        public int Nic { get; set; }
        public string PatchJson { get; set; }
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string ErrorMsg { get; set; }
    }

    public class ArticleV65ChainResponse
    {
        public bool Status { get; set; }
        public string Env { get; set; }
        public int Total { get; set; }
        public int Applied { get; set; }
        public int Failed { get; set; }
        public List<ArticleV65ChainItemResult> Results { get; set; }
    }
}
