using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Generic
{
    /// <summary>
    /// Generic RFC proxy — calls ANY SAP RFC by name with arbitrary IM_ parameters.
    /// Returns the full SAP response including EX_RETURN structure.
    ///
    /// POST /api/rfc/proxy
    /// Body: {"bapiname":"ZWM_HU_MVT_BIN_VAL_RFC","IM_USER":"250","IM_PLANT":"1000","IM_BIN":"001-001-01"}
    /// Response: {"EX_RETURN":{"TYPE":"S","MESSAGE":"Bin validated"},"EX_TANUM":"123",...}
    ///
    /// POST /api/rfc/proxy?env=prod   → uses production SAP (.170)
    /// POST /api/rfc/proxy?env=qa     → uses quality SAP (.179)
    /// POST /api/rfc/proxy            → uses dev SAP (.174) — default
    ///
    /// POST /api/rfc/proxy?strict=0   → legacy behaviour: execute even if a parameter
    ///                                  could not be applied. Default is strict (refuse).
    ///
    /// GET/POST /api/rfc/refresh?env=qa&amp;fm=NAME  → drop cached interface metadata for
    ///                                  one FM and re-read it from SAP. Use right after a
    ///                                  transport lands. Returns the live interface.
    ///
    /// Security: requires X-RFC-Key header = "v2-rfc-proxy-2026"
    /// </summary>
    public class GenericRfcProxyController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";

        [HttpPost]
        [Route("api/rfc/proxy")]
        public IHttpActionResult ProxyRfc([FromBody] JObject body)
        {
            try
            {
                // ── Auth check ──────────────────────────────────────────────
                if (!IsAuthorized())
                {
                    return Json(new
                    {
                        EX_RETURN = new { TYPE = "E", MESSAGE = "Unauthorized — missing or invalid X-RFC-Key" }
                    });
                }

                // ── Parse request ───────────────────────────────────────────
                if (body == null)
                {
                    return Json(new
                    {
                        EX_RETURN = new { TYPE = "E", MESSAGE = "Request body cannot be null" }
                    });
                }

                string rfcName = body.Value<string>("bapiname") ?? "";
                if (string.IsNullOrWhiteSpace(rfcName))
                {
                    return Json(new
                    {
                        EX_RETURN = new { TYPE = "E", MESSAGE = "bapiname is required" }
                    });
                }

                // ── Select SAP environment ──────────────────────────────────
                string env = System.Web.HttpContext.Current?.Request?.QueryString["env"] ?? "dev";
                RfcConfigParameters rfcPar = ResolveRfcParams(env);

                // ── Connect and invoke (with NCo self-heal on shut-down/invalid dest) ───
                RfcDestination dest = GetDestinationWithSelfHeal(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction(rfcName);

                // Set all IM_ parameters from the request body.
                // Track per-key parse errors so caller can see silent drops instead of
                // getting a "success" response with zero SAP writes.
                JArray paramApplied;
                JArray paramErrors;
                ApplyParams(myfun, body, out paramApplied, out paramErrors);

                // ── Stale-metadata self-heal ────────────────────────────────
                // NCo caches an FM's interface per destination. After a transport adds a
                // parameter, the cached copy still lacks it: SetValue throws "Element X of
                // container metadata Y unknown", the param is dropped, and SAP silently
                // receives spaces. Cost on 2026-08-13 — a day spent chasing a phantom SAP
                // bug for IM_ZONE on ZWM_PTL_GRT_HUB_CRATE_VLDT in QA, cleared only by an
                // app-pool recycle. Drop this one FM's metadata, re-read it from SAP and
                // re-apply the parameters: ~1s, no recycle, no human.
                bool metadataRefreshed = false;
                if (HasStaleMetadataError(paramErrors))
                {
                    try
                    {
                        rfcrep.RemoveFunctionMetadata(rfcName);
                        myfun = rfcrep.CreateFunction(rfcName);
                        ApplyParams(myfun, body, out paramApplied, out paramErrors);
                        metadataRefreshed = true;
                    }
                    catch (Exception mex)
                    {
                        paramErrors.Add(new JObject
                        {
                            ["key"] = "_METADATA_REFRESH",
                            ["error"] = mex.Message
                        });
                    }
                }

                // ── Strict gate ─────────────────────────────────────────────
                // A parameter that could not be applied means SAP is about to run on a
                // half-filled payload. Refuse the call rather than return a plausible
                // business error built from missing input. Escape hatch for a legacy
                // caller: ?strict=0 on the query string, or appSetting
                // RfcProxy.StrictParams=false in Web.config.
                if (paramErrors.Count > 0 && IsStrictParams())
                {
                    JObject strictErr = new JObject
                    {
                        ["EX_RETURN"] = new JObject
                        {
                            ["TYPE"] = "E",
                            ["MESSAGE"] = "RFC " + rfcName + " NOT executed — " + paramErrors.Count +
                                          " input parameter(s) could not be applied. Sending a partial " +
                                          "payload would produce a wrong result silently. See _PARAM_ERRORS."
                        },
                        ["_RFC_NAME"] = rfcName,
                        ["_ENV"] = env,
                        ["_PARAMS_APPLIED"] = paramApplied,
                        ["_PARAM_ERRORS"] = paramErrors,
                        ["_METADATA_REFRESHED"] = metadataRefreshed,
                        ["_EXECUTED"] = false
                    };
                    return Content(HttpStatusCode.BadRequest, strictErr);
                }

                // Hard-fail if nothing at all was applied to the RFC — prevents the
                // "silent success + zero SAP writes" bug that hid V65 no-ops in QA.
                // Reachable only in lenient mode; the strict gate above fires first.
                if (paramApplied.Count == 0 && paramErrors.Count > 0)
                {
                    return Json(new JObject
                    {
                        ["EX_RETURN"] = new JObject
                        {
                            ["TYPE"] = "E",
                            ["MESSAGE"] = "All " + paramErrors.Count + " input parameters rejected by RFC " + rfcName + " — check names/types."
                        },
                        ["_PARAM_ERRORS"] = paramErrors,
                        ["_RFC_NAME"] = rfcName,
                        ["_ENV"] = env
                    });
                }

                myfun.Invoke(dest);

                // ── Build response with ALL export parameters ───────────────
                JObject result = new JObject();

                // Iterate over function metadata to get all exports
                for (int i = 0; i < myfun.Metadata.ParameterCount; i++)
                {
                    RfcParameterMetadata paramMeta = myfun.Metadata[i];

                    // Export, changing, and tables parameters (not import)
                    if (paramMeta.Direction == RfcDirection.EXPORT ||
                        paramMeta.Direction == RfcDirection.CHANGING ||
                        paramMeta.Direction == RfcDirection.TABLES)
                    {
                        string paramName = paramMeta.Name;

                        try
                        {
                            if (paramMeta.DataType == RfcDataType.STRUCTURE)
                            {
                                IRfcStructure structure = myfun.GetStructure(paramName);
                                JObject structObj = new JObject();
                                for (int j = 0; j < structure.Metadata.FieldCount; j++)
                                {
                                    string fieldName = structure.Metadata[j].Name;
                                    structObj[fieldName] = structure.GetString(fieldName);
                                }
                                result[paramName] = structObj;
                            }
                            else if (paramMeta.DataType == RfcDataType.TABLE)
                            {
                                IRfcTable table = myfun.GetTable(paramName);
                                JArray tableArr = new JArray();
                                foreach (IRfcStructure row in table)
                                {
                                    JObject rowObj = new JObject();
                                    for (int j = 0; j < row.Metadata.FieldCount; j++)
                                    {
                                        string fieldName = row.Metadata[j].Name;
                                        rowObj[fieldName] = row.GetString(fieldName);
                                    }
                                    tableArr.Add(rowObj);
                                }
                                result[paramName] = tableArr;
                            }
                            else
                            {
                                result[paramName] = myfun.GetString(paramName);
                            }
                        }
                        catch
                        {
                            // Field read error — include as empty
                            result[paramName] = "";
                        }
                    }
                }

                // Ensure EX_RETURN exists (some RFCs don't have it)
                if (result["EX_RETURN"] == null)
                {
                    result["EX_RETURN"] = new JObject
                    {
                        ["TYPE"] = "S",
                        ["MESSAGE"] = "RFC executed successfully (no EX_RETURN defined)"
                    };
                }

                // Debug echo — caller can see exactly what the proxy fed to SAP.
                // Diagnoses silent drops (e.g. Pool B writing to V65 but AUSP empty)
                // without needing SAP-side trace access.
                result["_RFC_NAME"] = rfcName;
                result["_ENV"] = env;
                result["_PARAMS_APPLIED"] = paramApplied;
                if (paramErrors.Count > 0) result["_PARAM_ERRORS"] = paramErrors;
                if (metadataRefreshed) result["_METADATA_REFRESHED"] = true;

                return Json(result);
            }
            catch (RfcAbapException ex)
            {
                return Json(new
                {
                    EX_RETURN = new { TYPE = "E", MESSAGE = "SAP ABAP error: " + ex.Message }
                });
            }
            catch (RfcCommunicationException ex)
            {
                return Json(new
                {
                    EX_RETURN = new { TYPE = "E", MESSAGE = "SAP connection error: " + ex.Message }
                });
            }
            catch (RfcLogonException ex)
            {
                return Json(new
                {
                    EX_RETURN = new { TYPE = "E", MESSAGE = "SAP logon error: " + ex.Message }
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    EX_RETURN = new { TYPE = "E", MESSAGE = "Error: " + ex.Message }
                });
            }
        }

        /// <summary>
        /// Force-refresh the cached interface of one function module (or of every
        /// function, with all=1) on one environment, then return what SAP actually
        /// exposes. Run this straight after a transport that changes an FM signature
        /// — it is the surgical alternative to recycling V2RfcTestPool, and it doubles
        /// as proof of which parameters the proxy can now see.
        ///
        /// GET/POST /api/rfc/refresh?env=qa&amp;fm=ZWM_PTL_GRT_HUB_CRATE_VLDT
        /// GET/POST /api/rfc/refresh?env=qa&amp;all=1
        /// </summary>
        [HttpGet]
        [HttpPost]
        [Route("api/rfc/refresh")]
        public IHttpActionResult RefreshMetadata()
        {
            try
            {
                if (!IsAuthorized())
                {
                    return Json(new
                    {
                        EX_RETURN = new { TYPE = "E", MESSAGE = "Unauthorized — missing or invalid X-RFC-Key" }
                    });
                }

                var qs = System.Web.HttpContext.Current?.Request?.QueryString;
                string env = qs?["env"] ?? "dev";
                string fm = (qs?["fm"] ?? "").Trim().ToUpperInvariant();
                bool all = (qs?["all"] ?? "") == "1";

                if (string.IsNullOrWhiteSpace(fm) && !all)
                {
                    return Json(new
                    {
                        EX_RETURN = new { TYPE = "E", MESSAGE = "fm=<FUNCTION_NAME> is required (or all=1 to clear every cached function)" }
                    });
                }

                RfcDestination dest = GetDestinationWithSelfHeal(ResolveRfcParams(env));
                RfcRepository rfcrep = dest.Repository;

                if (all)
                {
                    rfcrep.ClearFunctionMetadata();
                    rfcrep.ClearStructureMetadata();
                    rfcrep.ClearTableMetadata();
                    return Json(new JObject
                    {
                        ["EX_RETURN"] = new JObject
                        {
                            ["TYPE"] = "S",
                            ["MESSAGE"] = "All cached function/structure/table metadata cleared for env " + env
                        },
                        ["_ENV"] = env
                    });
                }

                rfcrep.RemoveFunctionMetadata(fm);

                // Re-read straight from SAP so the caller sees the live signature.
                RfcFunctionMetadata meta = rfcrep.GetFunctionMetadata(fm);
                JArray iface = new JArray();
                for (int i = 0; i < meta.ParameterCount; i++)
                {
                    RfcParameterMetadata p = meta[i];
                    iface.Add(new JObject
                    {
                        ["name"] = p.Name,
                        ["direction"] = p.Direction.ToString(),
                        ["type"] = p.DataType.ToString(),
                        ["length"] = p.NucLength
                    });
                }

                return Json(new JObject
                {
                    ["EX_RETURN"] = new JObject
                    {
                        ["TYPE"] = "S",
                        ["MESSAGE"] = "Metadata refreshed for " + fm + " on env " + env + " — " + iface.Count + " parameter(s) live"
                    },
                    ["_RFC_NAME"] = fm,
                    ["_ENV"] = env,
                    ["_INTERFACE"] = iface
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    EX_RETURN = new { TYPE = "E", MESSAGE = "Refresh failed: " + ex.Message }
                });
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private bool IsAuthorized()
        {
            IEnumerable<string> keyHeaders;
            bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
            return hasKey && keyHeaders.FirstOrDefault() == API_KEY;
        }

        private static RfcConfigParameters ResolveRfcParams(string env)
        {
            switch ((env ?? "dev").ToLower())
            {
                // Production host and client, authenticating as SAP_CLOUDAI. Kept off "prod"
                // deliberately: the fixed-asset run needs A_S_ANLKL, which POWERBI lacks, and
                // every other production consumer must keep running - and keep stamping -
                // POWERBI. See BaseController.rfcConfigparametersproductionfa.
                case "prodfa":
                    return BaseController.rfcConfigparametersproductionfa();
                case "prod":
                case "production":
                    return BaseController.rfcConfigparametersproduction();
                case "qa":
                case "quality":
                    return BaseController.rfcConfigparametersquality();
                default:
                    return BaseController.rfcConfigparameters();
            }
        }

        /// <summary>
        /// Keys that steer the proxy itself rather than the RFC. Skipped without being
        /// counted as errors, so a caller that puts env/strict in the body is not
        /// rejected by the strict gate.
        /// </summary>
        private static bool IsControlKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return true;
            if (key.StartsWith("_", StringComparison.Ordinal)) return true;
            return key.Equals("bapiname", StringComparison.OrdinalIgnoreCase)
                || key.Equals("env", StringComparison.OrdinalIgnoreCase)
                || key.Equals("strict", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Strict by default: refuse to invoke when any parameter could not be applied.
        /// Override per call with ?strict=0, or globally with appSetting
        /// RfcProxy.StrictParams=false (Web.config edit, no redeploy).
        /// </summary>
        private static bool IsStrictParams()
        {
            string q = System.Web.HttpContext.Current?.Request?.QueryString["strict"];
            if (!string.IsNullOrEmpty(q))
            {
                if (q == "0" || q.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                if (q == "1" || q.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            }

            string cfg = ConfigurationManager.AppSettings["RfcProxy.StrictParams"];
            if (!string.IsNullOrEmpty(cfg) &&
                (cfg == "0" || cfg.Equals("false", StringComparison.OrdinalIgnoreCase))) return false;

            return true;
        }

        /// <summary>
        /// True when a parameter failed because the cached interface does not know it —
        /// the fingerprint of metadata that predates a transport.
        /// NCo wording: "Element IM_ZONE of container metadata ZFOO unknown".
        /// </summary>
        private static bool HasStaleMetadataError(JArray errors)
        {
            if (errors == null) return false;
            foreach (JToken e in errors)
            {
                string msg = (string)e["error"] ?? "";
                if (msg.IndexOf("container metadata", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("not a member", StringComparison.OrdinalIgnoreCase) >= 0
                    || (msg.IndexOf("metadata", StringComparison.OrdinalIgnoreCase) >= 0
                        && msg.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Apply every body key to the RFC function. Pure — call it again after a
        /// metadata refresh to re-apply against the fresh interface.
        /// </summary>
        private static void ApplyParams(IRfcFunction myfun, JObject body, out JArray applied, out JArray errors)
        {
            JArray paramApplied = new JArray();
            JArray paramErrors = new JArray();

            foreach (var prop in body.Properties())
            {
                string key = prop.Name;
                if (IsControlKey(key)) continue;

                try
                {
                    // TABLE param: JSON array of row objects → GetTable + Append
                    if (prop.Value is JArray arr)
                    {
                        IRfcTable rfcTable = myfun.GetTable(key);
                        int rowIdx = 0;
                        foreach (JObject rowObj in arr)
                        {
                            IRfcStructure row = rfcTable.Metadata.LineType.CreateStructure();
                            foreach (var field in rowObj.Properties())
                            {
                                try { row.SetValue(field.Name, field.Value.ToString()); }
                                catch (Exception fex)
                                {
                                    paramErrors.Add(new JObject
                                    {
                                        ["key"] = key + "[" + rowIdx + "]." + field.Name,
                                        ["error"] = fex.Message
                                    });
                                }
                            }
                            rfcTable.Append(row);
                            rowIdx++;
                        }
                        paramApplied.Add(key + " (table," + rowIdx + " rows)");
                    }
                    // STRUCTURE param: nested JSON object → GetStructure + SetValue per field.
                    // Per commit 13b60db upstream. Extended here to surface per-field errors
                    // instead of silent catch — helps diagnose typos in nested field names.
                    // Same null-safety as scalar: skip empty, "BLANK" → "".
                    else if (prop.Value is JObject nestedObj)
                    {
                        IRfcStructure rfcStruct = myfun.GetStructure(key);
                        foreach (var field in nestedObj.Properties())
                        {
                            string fval = field.Value.Type == JTokenType.Null ? null : field.Value.ToString();
                            if (string.IsNullOrEmpty(fval)) continue;
                            if (fval.Equals("BLANK", StringComparison.Ordinal)) fval = "";
                            try { rfcStruct.SetValue(field.Name, fval); }
                            catch (Exception fex)
                            {
                                paramErrors.Add(new JObject
                                {
                                    ["key"] = key + "." + field.Name,
                                    ["error"] = fex.Message
                                });
                            }
                        }
                        paramApplied.Add(key + " (structure)");
                    }
                    // Scalar param: string/number/bool → SetValue.
                    // Null-safety: omitted/empty values are NOT forwarded — leaves
                    // SAP field untouched. Sentinel "BLANK" → explicit clear ("").
                    else
                    {
                        string sval = prop.Value.Type == JTokenType.Null ? null : prop.Value.ToString();
                        if (string.IsNullOrEmpty(sval))
                        {
                            paramApplied.Add(key + " (skipped:empty)");
                        }
                        else
                        {
                            if (sval.Equals("BLANK", StringComparison.Ordinal)) sval = "";
                            myfun.SetValue(key, sval);
                            paramApplied.Add(key);
                        }
                    }
                }
                catch (Exception pex)
                {
                    paramErrors.Add(new JObject
                    {
                        ["key"] = key,
                        ["error"] = pex.Message
                    });
                }
            }

            applied = paramApplied;
            errors = paramErrors;
        }

        /// <summary>
        /// GetDestination with self-heal: if the cached RfcDestination is in a
        /// "shut-down"/"invalid"/"REPLACED" state (a known NCo SDK bug exposed by
        /// env-switching and cold-start races), unregister it so NCo can create
        /// a fresh handle, then retry once. Without this, a single transient NCo
        /// failure wedges the entire IIS worker until the app pool is recycled.
        /// </summary>
        private static readonly object _selfHealLock = new object();
        private static RfcDestination GetDestinationWithSelfHeal(RfcConfigParameters rfcPar)
        {
            try
            {
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                dest.Ping();
                return dest;
            }
            catch (Exception ex)
            {
                string msg = ex.Message ?? string.Empty;
                bool isWedged = msg.IndexOf("shut-down", StringComparison.OrdinalIgnoreCase) >= 0
                              || msg.IndexOf("invalid destination", StringComparison.OrdinalIgnoreCase) >= 0
                              || msg.IndexOf("REPLACED", StringComparison.OrdinalIgnoreCase) >= 0
                              || msg.IndexOf("Cannot obtain system attributes", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isWedged) throw;

                lock (_selfHealLock)
                {
                    // NCo caches destinations by Name. A wedged dest stays cached forever.
                    // Force a fresh one by cloning params with a new unique Name.
                    RfcConfigParameters fresh = CloneWithFreshName(rfcPar);
                    System.Threading.Thread.Sleep(200);
                    RfcDestination dest2 = RfcDestinationManager.GetDestination(fresh);
                    dest2.Ping();
                    return dest2;
                }
            }
        }

        /// <summary>
        /// Clone RfcConfigParameters with a new unique Name to force NCo to
        /// build a fresh destination instead of returning the cached (wedged) one.
        /// </summary>
        private static RfcConfigParameters CloneWithFreshName(RfcConfigParameters src)
        {
            RfcConfigParameters fresh = new RfcConfigParameters();
            string[] keys = new string[]
            {
                RfcConfigParameters.AppServerHost,
                RfcConfigParameters.Client,
                RfcConfigParameters.User,
                RfcConfigParameters.Password,
                RfcConfigParameters.SystemID,
                RfcConfigParameters.SystemNumber,
                RfcConfigParameters.Language,
            };
            foreach (string k in keys)
            {
                try
                {
                    string v = src[k];
                    if (!string.IsNullOrEmpty(v)) fresh.Add(k, v);
                }
                catch { }
            }
            string origName = null;
            try { origName = src[RfcConfigParameters.Name]; } catch { }
            if (string.IsNullOrEmpty(origName)) origName = "Connection";
            fresh.Add(RfcConfigParameters.Name, origName + "_heal_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            return fresh;
        }

        /// <summary>
        /// Health check — verifies SAP connectivity without calling an RFC.
        /// GET /api/rfc/proxy/health
        /// </summary>
        [HttpGet]
        [Route("api/rfc/proxy/health")]
        public IHttpActionResult Health()
        {
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                dest.Ping();
                return Json(new
                {
                    status = "ok",
                    host = "192.168.144.174",
                    client = "210",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    status = "error",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                });
            }
        }
    }
}
