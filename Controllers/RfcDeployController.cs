using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;

namespace Vendor_SRM_Routing_Application.Controllers
{
    /// <summary>
    /// RfcDeployController - MVC Controller for the RFC Automation Portal.
    /// Routes:
    ///   GET  /RfcDeploy              -> Index()        Upload form (docx -> AI -> C# -> GitHub -> IIS)
    ///   POST /RfcDeploy/Upload       -> Upload()       Proxy file to Cloudflare Worker pipeline
    ///   GET  /RfcDeploy/Status/{id}  -> Status()       Poll job status from CF Worker KV
    ///   GET  /RfcDeploy/Explorer     -> Explorer()     Human-readable Swagger-style API explorer
    ///   GET  /RfcDeploy/SwaggerUI    -> SwaggerUI()    Embedded Swagger UI powered by OpenAPI spec
    ///   GET  /RfcDeploy/OpenApiJson  -> OpenApiJson()  OpenAPI 3.0 JSON spec for tools/Postman
    ///   GET  /RfcDeploy/Manifest     -> Manifest()     Simplified JSON manifest
    ///   GET  /RfcDeploy/Swagger      -> Swagger()      Native Swashbuckle redirect
    /// </summary>
    public class RfcDeployController : System.Web.Mvc.Controller
    {
        private const string WORKER_BASE = "https://v2-rfc-pipeline.akash-bab.workers.dev";
        private const string IIS_BASE    = "http://v2retail.net:9005";
        private const string DAB_BASE    = "https://my-dab-app.azurewebsites.net";
        private const string GH_BASE     = "https://github.com/akash0631/rfc-api/blob/master/";

        // GET /RfcDeploy
        public ActionResult Index()
        {
            ViewBag.Title      = "V2 Retail - RFC Auto-Deploy";
            ViewBag.WorkerBase = WORKER_BASE;
            return View();
        }

        // POST /RfcDeploy/Upload
        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file, string env = "dev")
        {
            if (file == null || file.ContentLength == 0)
                return Json(new { success = false, error = "No file uploaded." });
            try
            {
                using (var client = new HttpClient())
                using (var form   = new MultipartFormDataContent())
                {
                    var bytes = new byte[file.ContentLength];
                    file.InputStream.Read(bytes, 0, file.ContentLength);
                    var fc = new ByteArrayContent(bytes);
                    fc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    form.Add(fc, "file", file.FileName);
                    form.Add(new StringContent(env), "env");

                    var r    = await client.PostAsync(WORKER_BASE + "/deploy", form);
                    var json = await r.Content.ReadAsStringAsync();
                    if (!r.IsSuccessStatusCode)
                        return Json(new { success = false, error = "Worker: " + json });

                    var d = JsonConvert.DeserializeObject<JObject>(json);
                    return Json(new { success = true, jobId = d?["jobId"]?.ToString(), status = d?["status"]?.ToString() });
                }
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }

        // GET /RfcDeploy/Status/{id}
        [HttpGet]
        public async Task<ActionResult> Status(string id)
        {
            if (string.IsNullOrEmpty(id)) return Json(new { error = "Job ID required" }, JsonRequestBehavior.AllowGet);
            try
            {
                using (var client = new HttpClient())
                {
                    var r = await client.GetAsync(WORKER_BASE + "/status/" + id);
                    return Content(await r.Content.ReadAsStringAsync(), "application/json");
                }
            }
            catch (Exception ex) { return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet); }
        }

        // GET /RfcDeploy/Explorer
        public ActionResult Explorer()
        {
            ViewBag.Title      = "V2 Retail - RFC API Explorer";
            ViewBag.IISBase    = IIS_BASE;
            ViewBag.DABBase    = DAB_BASE;
            ViewBag.GHBase     = GH_BASE;
            var endpoints      = GetAllEndpoints();
            ViewBag.TotalCount = endpoints.Count;
            ViewBag.GroupCount = endpoints.Select(e => e.Group).Distinct().Count();
            ViewBag.ParamCount = endpoints.Sum(e => e.Parameters.Count);
            return View(endpoints);
        }

        // GET /RfcDeploy/SwaggerUI
        public ActionResult SwaggerUI()
        {
            ViewBag.Title      = "V2 Retail - Swagger UI";
            ViewBag.OpenApiUrl = Url.Action("OpenApiJson", "RfcDeploy", null, Request.Url.Scheme);
            return View();
        }

        // GET /RfcDeploy/OpenApiJson  - OpenAPI 3.0 spec
        [HttpGet]
        public ActionResult OpenApiJson()
        {
            var endpoints = GetAllEndpoints();
            var paths     = new JObject();

            foreach (var ep in endpoints)
            {
                var reqProps = new JObject();
                foreach (var p in ep.Parameters)
                {
                    var prop = new JObject
                    {
                        ["type"]        = p.IsTable ? "array" : "string",
                        ["description"] = (p.Description ?? p.Name) + (string.IsNullOrEmpty(p.SapType) ? "" : " [SAP: " + p.SapType + "]")
                    };
                    if (p.IsTable) prop["items"] = new JObject { ["type"] = "object" };
                    reqProps[p.Name] = prop;
                }

                var respProps = new JObject
                {
                    ["Status"]  = new JObject { ["type"] = "boolean" },
                    ["Message"] = new JObject { ["type"] = "string"  }
                };
                if (ep.ResponseTables.Any(t => t.Contains("ET_DATA") || t.Contains("ZTT_")))
                    respProps["Data"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "object" } };

                paths["/" + ep.Route] = new JObject
                {
                    ["post"] = new JObject
                    {
                        ["tags"]        = new JArray(ep.Group),
                        ["operationId"] = ep.Name,
                        ["summary"]     = ep.Description,
                        ["description"] = "SAP RFC: `" + ep.SapRfc + "` Host: `" + ep.SapHost + "` Client: `" + ep.Client + "`" +
                                          (ep.DataLakeEndpoint != null ? "\n\nData Lake: " + ep.DataLakeEndpoint : ""),
                        ["requestBody"] = new JObject
                        {
                            ["required"] = true,
                            ["content"]  = new JObject
                            {
                                ["application/json"] = new JObject
                                {
                                    ["schema"] = new JObject
                                    {
                                        ["type"]       = "object",
                                        ["properties"] = reqProps,
                                        ["required"]   = new JArray(ep.Parameters.Where(p => p.Required).Select(p => (object)p.Name).ToArray())
                                    }
                                }
                            }
                        },
                        ["responses"] = new JObject
                        {
                            ["200"] = new JObject
                            {
                                ["description"] = "Success",
                                ["content"]     = new JObject
                                {
                                    ["application/json"] = new JObject
                                    {
                                        ["schema"] = new JObject { ["type"] = "object", ["properties"] = respProps }
                                    }
                                }
                            },
                            ["400"] = new JObject { ["description"] = "SAP error (TYPE=E in EX_RETURN)" },
                            ["500"] = new JObject { ["description"] = "IIS / SAP connector exception" }
                        },
                        ["x-source-file"] = ep.FilePath ?? "",
                        ["x-data-lake"]   = ep.DataLakeEndpoint ?? ""
                    }
                };
            }

            var spec = new JObject
            {
                ["openapi"] = "3.0.1",
                ["info"]    = new JObject
                {
                    ["title"]       = "V2 Retail - SAP RFC REST API",
                    ["version"]     = "1.0.0",
                    ["description"] = endpoints.Count + " SAP RFC endpoints on IIS (V2DC-ADDVERB). CI/CD via GitHub Actions.",
                    ["contact"]     = new JObject { ["name"] = "V2 Retail Tech", ["url"] = "https://github.com/akash0631/rfc-api" }
                },
                ["servers"] = new JArray
                {
                    new JObject { ["url"] = IIS_BASE, ["description"] = "IIS Primary (192.168.151.24)" },
                    new JObject { ["url"] = "http://192.168.144.174:9005", ["description"] = "SAP Dev (Client 210)" }
                },
                ["tags"]  = new JArray(endpoints.Select(e => e.Group).Distinct()
                              .Select(g => (object)new JObject { ["name"] = g }).ToArray()),
                ["paths"] = paths
            };

            return Content(spec.ToString(Formatting.Indented), "application/json");
        }

        // GET /RfcDeploy/Manifest
        [HttpGet]
        public ActionResult Manifest()
        {
            var eps = GetAllEndpoints();
            return Json(new
            {
                version        = "1.0",
                generatedAt    = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                baseUrl        = IIS_BASE,
                totalEndpoints = eps.Count,
                endpoints      = eps
            }, JsonRequestBehavior.AllowGet);
        }

        // GET /RfcDeploy/Swagger -> native Swashbuckle
        public ActionResult Swagger() => Redirect("/swagger/ui/index");

        // =====================================================================
        // ENDPOINT MANIFEST
        // =====================================================================
        private List<RfcEndpoint> GetAllEndpoints()
        {
            return new List<RfcEndpoint>
            {
                // ── Inbound: HU Scanning & Unloading ─────────────────────────
                new RfcEndpoint
                {
                    Name = "ZVND_UNLOAD_HU_VALIDATE_RFC", Route = "api/ZVND_UNLOAD_HU_VALIDATE_RFC",
                    Group = "Inbound — Unloading", SapRfc = "ZVND_UNLOAD_HU_VALIDATE_RFC",
                    Description = "Validate scanned HU during vehicle unloading. Returns PO, invoice and vendor name.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",  SapType="WWWOBJID", Description="Logged-in user ID" },
                        new RfcParam { Name="IM_PLANT", SapType="WERKS_D",  Description="Plant/DC (auto-fetched)" },
                        new RfcParam { Name="IM_HU",    SapType="ZEXT_HU",  Description="Scanned HU number" }
                    },
                    ResponseTables = new List<string> { "ET_DATA (ZTT_VEN_BOX)", "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_UNLOAD_HU_VALIDATE_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_UNLOAD_PALLATE_VALIDATION", Route = "api/ZVND_UNLOAD_PALLATE_VALIDATION",
                    Group = "Inbound — Unloading", SapRfc = "ZVND_UNLOAD_PALLATE_VALIDATION",
                    Description = "Validate scanned Palette after HU validation. Confirms palette ready for putway.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",  SapType="WWWOBJID" },
                        new RfcParam { Name="IM_PLANT", SapType="WERKS_D"  },
                        new RfcParam { Name="IM_HU",    SapType="ZEXT_HU",  Description="Validated HU number" },
                        new RfcParam { Name="IM_PALL",  SapType="ZZPALETTE",Description="Scanned palette number" }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_UNLOAD_PALLATE_VALIDATIONController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_UNLOAD_SAVE_RFC", Route = "api/ZVND_UNLOAD_SAVE_RFC",
                    Group = "Inbound — Unloading", SapRfc = "ZVND_UNLOAD_SAVE_RFC",
                    Description = "Save unloading data to SAP after HU and Palette validated. Table: PLANT, VEHICLE, EXT_HU, PALETTE, PO_NO, BILL_NO.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",  SapType="WWWOBJID" },
                        new RfcParam { Name="IM_PARMS", SapType="ZTT_UNLOAD_SAVE",
                                       Description="Table: PLANT(WERKS_D), VEHICLE(ZVEH), EXT_HU(ZEXT_HU), PALETTE(ZZPALETTE), PO_NO(EBELN), BILL_NO(ZBILL_NO)",
                                       IsTable=true }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_UNLOAD_SAVE_RFCController.cs"
                },
                // ── Inbound: Putway to Bin ────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZVND_PUTWAY_BIN_VAL_RFC", Route = "api/ZVND_PUTWAY_BIN_VAL_RFC",
                    Group = "Inbound — Putway to Bin", SapRfc = "ZVND_PUTWAY_BIN_VAL_RFC",
                    Description = "Validate BIN location (LGPLA) for inbound putway. BIN shown greyed on device after success.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",  SapType="WWWOBJID" },
                        new RfcParam { Name="IM_PLANT", SapType="WERKS_D"  },
                        new RfcParam { Name="IM_BIN",   SapType="LGPLA",    Description="Scanned BIN location" }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_PUTWAY_BIN_VAL_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_PUTWAY_PALETTE_VAL_RFC", Route = "api/ZVND_PUTWAY_PALETTE_VAL_RFC",
                    Group = "Inbound — Putway to Bin", SapRfc = "ZVND_PUTWAY_PALETTE_VAL_RFC",
                    Description = "Validate Palette for inbound putway. Returns vendor info, PO, invoice and total box count on palette.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",  SapType="WWWOBJID"  },
                        new RfcParam { Name="IM_PLANT", SapType="WERKS_D"   },
                        new RfcParam { Name="IM_BIN",   SapType="ZEXT_HU",  Description="Validated bin (HU type)" },
                        new RfcParam { Name="IM_PALL",  SapType="ZZPALETTE",Description="Scanned palette number" }
                    },
                    ResponseTables = new List<string> { "ET_DATA (ZTT_VEN_BOX)", "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_PUTWAY_PALETTE_VAL_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_PUTWAY_SAVE_DATA_RFC", Route = "api/ZVND_PUTWAY_SAVE_DATA_RFC",
                    Group = "Inbound — Putway to Bin", SapRfc = "ZVND_PUTWAY_SAVE_DATA_RFC",
                    Description = "Save putway data after BIN+Palette validated. After save: palette+bin clear, PO/Vendor info persists.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER", SapType="WWWOBJID" },
                        new RfcParam { Name="IT_DATA", SapType="ZTT_PUTWAY_SAVE",
                                       Description="Table: PLANT, BIN(LGPLA), PALETTE(ZZPALETTE), EXT_HU, PO_NO(EBELN), BILL_NO(ZBILL_NO)",
                                       IsTable=true }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_PUTWAY_SAVE_DATA_RFCController.cs"
                },
                // ── HU Creation ───────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZVND_HU_PUSH_API_POST", Route = "api/ZVND_HU_PUSH_API_POST",
                    Group = "HU Creation", SapRfc = "ZVND_HU_PUSH_API_POST",
                    Description = "Push HU creation data (list) to SAP. Called by vendor mobile app on HU label print.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="HU_NO",         SapType="CHAR"    },
                        new RfcParam { Name="PO_NO",         SapType="EBELN"   },
                        new RfcParam { Name="ARTICLE_NO",    SapType="MATNR"   },
                        new RfcParam { Name="DESIGN",        SapType="CHAR"    },
                        new RfcParam { Name="QUANTITY",      SapType="MENGE"   },
                        new RfcParam { Name="VENDOR_CODE",   SapType="LIFNR"   },
                        new RfcParam { Name="EAN",           SapType="EAN11"   },
                        new RfcParam { Name="CREATION_DATE", SapType="DATS"    },
                        new RfcParam { Name="CREATION_TIME", SapType="TIMS"    },
                        new RfcParam { Name="CREATION_USER", SapType="UNAME"   },
                        new RfcParam { Name="STATUS",        SapType="CHAR",    Required=false },
                        new RfcParam { Name="INV_NO",        SapType="ZBILL_NO",Required=false }
                    },
                    ResponseTables = new List<string> { "ES_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/HU_Creation/ZVND_HU_PUSH_API_POSTController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_HU_VALIDATE_RFC", Route = "api/ZVND_HU_VALIDATE_RFC",
                    Group = "HU Creation", SapRfc = "ZVND_HU_VALIDATE_RFC",
                    Description = "Validate HU number against a PO. Returns store-wise dispatch data.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",      SapType="UNAME"   },
                        new RfcParam { Name="IM_HU_NUMBER", SapType="ZEXT_HU" },
                        new RfcParam { Name="IM_PO",        SapType="EBELN"   }
                    },
                    ResponseTables = new List<string> { "ET_STORES", "ET_EAN_DATA", "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/HU_Creation/ZVND_HU_VALIDATE_RFCController.cs"
                },
                // ── Finance ───────────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZADVANCE_PAYMENT_RFC", Route = "api/ZADVANCE_PAYMENT_RFC",
                    Group = "Finance", SapRfc = "ZADVANCE_PAYMENT_RFC",
                    Description = "Fetch advance payment documents for a company code within a posting date range.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="I_COMPANY_CODE",      SapType="BUKRS", Description="Company code e.g. 1000" },
                        new RfcParam { Name="I_POSTING_DATE_LOW",  SapType="BUDAT", Description="Start date YYYYMMDD" },
                        new RfcParam { Name="I_POSTING_DATE_HIGH", SapType="BUDAT", Description="End date YYYYMMDD" }
                    },
                    ResponseTables = new List<string> { "IT_FINAL", "EX_RETURN (BAPIRET2)" },
                    DataLakeEndpoint = DAB_BASE + "/api/ET_ADVANCE_PAYMENT",
                    FilePath = "Controllers/Finance/ZADVANCE_PAYMENT_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZFINANCE_DOCUMENT_RFC", Route = "api/ZFINANCE_DOCUMENT_RFC",
                    Group = "Finance", SapRfc = "ZFINANCE_DOCUMENT_RFC",
                    Description = "Finance document retrieval with posting date range filter.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="I_COMPANY_CODE",      SapType="BUKRS" },
                        new RfcParam { Name="I_POSTING_DATE_LOW",  SapType="DATS"  },
                        new RfcParam { Name="I_POSTING_DATE_HIGH", SapType="DATS"  }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    DataLakeEndpoint = DAB_BASE + "/api/ET_FINANCE_DOCUMENTS",
                    FilePath = "Controllers/Finance/ZFINANCE_DOCUMENT_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZSALES_MOP_RFC", Route = "api/ZSALES_MOP_RFC",
                    Group = "Finance", SapRfc = "ZSALES_MOP_RFC",
                    Description = "Mode-of-payment wise sales data for a date range.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_DATE_LOW",  SapType="DATS", Description="Start date YYYYMMDD" },
                        new RfcParam { Name="IM_DATE_HIGH", SapType="DATS", Description="End date YYYYMMDD"   }
                    },
                    ResponseTables = new List<string> { "ET_DATA", "EX_RETURN (BAPIRET2)" },
                    DataLakeEndpoint = DAB_BASE + "/api/ET_SALES_MOP",
                    FilePath = "Controllers/Finance/ZSALES_MOP_RFCController.cs"
                },
                // ── Store Master ──────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZGET_STORE_MASTER_RFC", Route = "api/ZGET_STORE_MASTER_RFC",
                    Group = "Store Master", SapRfc = "ZGET_STORE_MASTER_RFC",
                    Description = "Fetch store master data from SAP by store code.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IV_STORE_CODE", SapType="CHAR10", Description="Store code e.g. DL01" }
                    },
                    ResponseTables = new List<string> { "ET_DATA", "EX_RETURN (BAPIRET2)" },
                    DataLakeEndpoint = DAB_BASE + "/api/ET_ZGET_STORE_MASTER",
                    FilePath = "Controllers/Vendor_SRM_Routing/ZGET_STORE_MASTER_RFCController.cs"
                },
                // ── Vendor SRM ────────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "PO_Detail", Route = "api/PO_Detail",
                    Group = "Vendor SRM — Routing", SapRfc = "ZSRM_PO_DETAIL",
                    Description = "Get article-wise PO detail for a vendor.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER", SapType="UNAME" },
                        new RfcParam { Name="IM_PO",   SapType="EBELN", Description="Purchase order number" }
                    },
                    ResponseTables = new List<string> { "ET_DATA", "ES_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Vendor_SRM_Routing/PO_DetailController.cs"
                },
                new RfcEndpoint
                {
                    Name = "RoutingStatusList", Route = "api/RoutingStatusList",
                    Group = "Vendor SRM — Routing", SapRfc = "ZSRM_PO_RFC_GET_ROUTING",
                    Description = "Full routing milestone status list for PO/design.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_PO_NO",  SapType="EBELN", Required=false },
                        new RfcParam { Name="IM_DESIGN", SapType="CHAR",  Required=false },
                        new RfcParam { Name="IM_SATNR",  SapType="MATNR", Required=false },
                        new RfcParam { Name="IM_PO",     SapType="EBELN", Required=false }
                    },
                    ResponseTables = new List<string> { "ET_DATA", "ES_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Vendor_SRM_Routing/RoutingStatusListController.cs"
                },
                new RfcEndpoint
                {
                    Name = "Update_Routing_Status", Route = "api/Update_Routing_Status",
                    Group = "Vendor SRM — Routing", SapRfc = "ZSRM_ROUTING_POST",
                    Description = "Post routing confirmation for a PO design/article milestone.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="PO_NO",     SapType="EBELN"  },
                        new RfcParam { Name="DESIGN_NO", SapType="CHAR"   },
                        new RfcParam { Name="QTY",       SapType="MENGE"  },
                        new RfcParam { Name="RTNO",      SapType="CHAR",  Description="Routing number" },
                        new RfcParam { Name="REMARKS",   SapType="CHAR",   Required=false },
                        new RfcParam { Name="IM_USER",   SapType="UNAME"  }
                    },
                    ResponseTables = new List<string> { "EX_DATA", "ES_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Vendor_SRM_Routing/Update_Routing_StatusController.cs"
                },
                // ── DC Routing ────────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZWM_GATE_ENTRY_RFC", Route = "api/ZWM_GATE_ENTRY_RFC",
                    Group = "DC Routing", SapRfc = "ZWM_GATE_ENTRY_RFC",
                    Description = "Get gate entry data for a PO. Returns lot ageing and pending quantities.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_EBELN",      SapType="EBELN" },
                        new RfcParam { Name="Gate_Entry_No", SapType="CHAR",  Required=false },
                        new RfcParam { Name="Quantity",      SapType="MENGE", Required=false }
                    },
                    ResponseTables = new List<string> { "ET_DATA", "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/DcRouting/ZWM_GATE_ENTRY_RFCController.cs"
                },
                // ── Paperless Picklist ────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "GetPicklistData", Route = "api/GetPicklistData",
                    Group = "Paperless Picklist", SapRfc = "ZWM_RFC_GET_PICKLIST_DATA",
                    Description = "Fetch picklist data by picklist number. Returns article/bin/quantity breakdown.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",  SapType="UNAME"   },
                        new RfcParam { Name="IM_WERKS", SapType="WERKS_D" },
                        new RfcParam { Name="IM_PICNR", SapType="CHAR",    Description="Picklist number" }
                    },
                    ResponseTables = new List<string> { "ET_DATA", "ET_EAN_DATA", "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/PaperlessPicklist/GetPicklistDataController.cs"
                },
                // ── Vehicle Loading ────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZWM_HU_SELECTION_RFC", Route = "api/ZWM_HU_SELECTION_RFC",
                    Group = "Vehicle Loading", SapRfc = "ZWM_HU_SELECTION_RFC",
                    Description = "Select HUs for vehicle loading. Returns HU list for vehicle/hub/store.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name="IM_USER",  SapType="UNAME"   },
                        new RfcParam { Name="IM_PLANT", SapType="WERKS_D" },
                        new RfcParam { Name="IM_VEH",   SapType="ZVEH",    Description="Vehicle registration number" },
                        new RfcParam { Name="IM_HUB",   SapType="CHAR",    Required=false }
                    },
                    ResponseTables = new List<string> { "ET_HULIST", "ET_ERROR" },
                    FilePath = "Controllers/Vehicle_Loading/ZWM_HU_SELECTION_RFCController.cs"
                }
            };
        }
    }

    // ── Model Classes ─────────────────────────────────────────────────────────
    public class RfcEndpoint
    {
        public string         Name             { get; set; }
        public string         Route            { get; set; }
        public string         Group            { get; set; }
        public string         SapRfc           { get; set; }
        public string         Environment      { get; set; } = "Dev";
        public string         SapHost          { get; set; } = "192.168.144.174";
        public string         Client           { get; set; } = "210";
        public string         Description      { get; set; }
        public List<RfcParam> Parameters       { get; set; } = new List<RfcParam>();
        public List<string>   ResponseTables   { get; set; } = new List<string>();
        public string         DataLakeEndpoint { get; set; }
        public string         FilePath         { get; set; }
        public string         Method           { get; set; } = "POST";
    }

    public class RfcParam
    {
        public string Name        { get; set; }
        public string SapType     { get; set; }
        public string Description { get; set; }
        public bool   Required    { get; set; } = true;
        public bool   IsTable     { get; set; } = false;
    }
}
