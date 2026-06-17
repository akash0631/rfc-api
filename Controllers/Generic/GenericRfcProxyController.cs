using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
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
                IEnumerable<string> keyHeaders;
                bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
                if (!hasKey || keyHeaders.FirstOrDefault() != API_KEY)
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
                RfcConfigParameters rfcPar;
                switch (env.ToLower())
                {
                    case "prod":
                    case "production":
                        rfcPar = BaseController.rfcConfigparametersproduction();
                        break;
                    case "qa":
                    case "quality":
                        rfcPar = BaseController.rfcConfigparametersquality();
                        break;
                    default:
                        rfcPar = BaseController.rfcConfigparameters();
                        break;
                }

                // ── Connect and invoke (with NCo self-heal on shut-down/invalid dest) ───
                RfcDestination dest = GetDestinationWithSelfHeal(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction(rfcName);

                // Set all IM_ parameters from the request body
                foreach (var prop in body.Properties())
                {
                    string key = prop.Name;
                    if (key.Equals("bapiname", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        // TABLE parameters: JSON array → GetTable + CreateStructure + Append
                        if (prop.Value is Newtonsoft.Json.Linq.JArray arr)
                        {
                            IRfcTable rfcTable = myfun.GetTable(key);
                            foreach (JObject rowObj in arr)
                            {
                                IRfcStructure row = rfcTable.Metadata.LineType.CreateStructure();
                                foreach (var field in rowObj.Properties())
                                {
                                    try { row.SetValue(field.Name, field.Value.ToString()); } catch { }
                                }
                                rfcTable.Append(row);
                            }
                        }
                        else
                        {
                            myfun.SetValue(key, prop.Value.ToString());
                        }
                    }
                    catch
                    {
                        // Parameter doesn't exist in RFC — skip
                    }
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
