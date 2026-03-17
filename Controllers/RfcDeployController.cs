using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Vendor_SRM_Routing_Application.Controllers
{
    /// <summary>
    /// RfcDeployController — MVC Controller for the RFC Automation Portal.
    ///
    /// Routes:
    ///   GET  /RfcDeploy              → Index()         Upload form
    ///   POST /RfcDeploy/Upload       → Upload()        Proxy to CF Worker pipeline
    ///   GET  /RfcDeploy/Status/{id}  → Status()        Poll job status from CF Worker
    ///   GET  /RfcDeploy/Explorer     → Explorer()      Swagger-like API explorer page
    ///   GET  /RfcDeploy/Manifest     → Manifest()      JSON list of all RFC endpoints
    ///   GET  /RfcDeploy/Swagger      → Swagger()       Native Swashbuckle redirect
    /// </summary>
    public class RfcDeployController : System.Web.Mvc.Controller
    {
        // Cloudflare Worker pipeline endpoint
        private const string WORKER_BASE = "https://v2-rfc-pipeline.akash-bab.workers.dev";

        // ─────────────────────────────────────────────────────────
        // GET /RfcDeploy  →  Upload form
        // ─────────────────────────────────────────────────────────
        public ActionResult Index()
        {
            ViewBag.Title = "V2 Retail · RFC Auto-Deploy";
            return View();
        }

        // ─────────────────────────────────────────────────────────
        // POST /RfcDeploy/Upload  →  Proxy file to CF Worker
        // Returns: { jobId, status } as JSON
        // ─────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file, string env = "dev")
        {
            if (file == null || file.ContentLength == 0)
                return Json(new { success = false, error = "No file uploaded." });

            try
            {
                using (var client = new HttpClient())
                using (var form = new MultipartFormDataContent())
                {
                    // Read uploaded file bytes
                    var fileBytes = new byte[file.ContentLength];
                    file.InputStream.Read(fileBytes, 0, file.ContentLength);

                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    form.Add(fileContent, "file", file.FileName);
                    form.Add(new StringContent(env), "env");

                    var response = await client.PostAsync(WORKER_BASE + "/deploy", form);
                    var json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                        return Json(new { success = false, error = "Worker error: " + json });

                    var result = JsonConvert.DeserializeObject<JObject>(json);
                    return Json(new
                    {
                        success = true,
                        jobId = result?["jobId"]?.ToString(),
                        status = result?["status"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // GET /RfcDeploy/Status/{jobId}  →  Poll CF Worker
        // Returns: raw job JSON (steps, status, rfcName, commit, etc.)
        // ─────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<ActionResult> Status(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Json(new { error = "Job ID required" }, JsonRequestBehavior.AllowGet);
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(WORKER_BASE + "/status/" + id);
                    var json = await response.Content.ReadAsStringAsync();
                    return Content(json, "application/json");
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ─────────────────────────────────────────────────────────
        // GET /RfcDeploy/Explorer  →  Swagger-like API explorer page
        // ─────────────────────────────────────────────────────────
        public ActionResult Explorer()
        {
            ViewBag.Title = "V2 Retail · RFC API Explorer";
            ViewBag.Endpoints = GetAllEndpoints();
            return View();
        }

        // ─────────────────────────────────────────────────────────
        // GET /RfcDeploy/Manifest  →  JSON manifest of all endpoints
        // Used by external tools, Postman, scripts
        // ─────────────────────────────────────────────────────────
        [HttpGet]
        public ActionResult Manifest()
        {
            var endpoints = GetAllEndpoints();
            return Json(new
            {
                version = "1.0",
                generatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                baseUrl = "http://v2retail.net:9005",
                totalEndpoints = endpoints.Count,
                endpoints
            }, JsonRequestBehavior.AllowGet);
        }

        // ─────────────────────────────────────────────────────────
        // GET /RfcDeploy/Swagger  →  Redirect to native Swashbuckle UI
        // ─────────────────────────────────────────────────────────
        public ActionResult Swagger()
        {
            return Redirect("/swagger/ui/index");
        }

        // ─────────────────────────────────────────────────────────
        // HELPER: Build the endpoint manifest from all known RFCs
        // ─────────────────────────────────────────────────────────
        private List<RfcEndpoint> GetAllEndpoints()
        {
            return new List<RfcEndpoint>
            {
                // ── Inbound: HU Scanning & Unloading ──────────────────────
                new RfcEndpoint
                {
                    Name = "ZVND_UNLOAD_HU_VALIDATE_RFC",
                    Route = "api/ZVND_UNLOAD_HU_VALIDATE_RFC",
                    Group = "Inbound — Unloading",
                    SapRfc = "ZVND_UNLOAD_HU_VALIDATE_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Validate scanned HU during vehicle unloading. Returns PO, invoice & vendor name.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "IM_USER",  SapType = "WWWOBJID", Description = "Logged-in user" },
                        new RfcParam { Name = "IM_PLANT", SapType = "WERKS_D",  Description = "Plant/DC (auto-fetched)" },
                        new RfcParam { Name = "IM_HU",    SapType = "ZEXT_HU",  Description = "Scanned HU number" }
                    },
                    ResponseTables = new List<string> { "ET_DATA (ZTT_VEN_BOX)", "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_UNLOAD_HU_VALIDATE_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_UNLOAD_PALLATE_VALIDATION",
                    Route = "api/ZVND_UNLOAD_PALLATE_VALIDATION",
                    Group = "Inbound — Unloading",
                    SapRfc = "ZVND_UNLOAD_PALLATE_VALIDATION",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Validate scanned Palette after HU validation. Confirms palette ready for putway.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "IM_USER",  SapType = "WWWOBJID",  Description = "Logged-in user" },
                        new RfcParam { Name = "IM_PLANT", SapType = "WERKS_D",   Description = "Plant/DC" },
                        new RfcParam { Name = "IM_HU",    SapType = "ZEXT_HU",   Description = "HU number" },
                        new RfcParam { Name = "IM_PALL",  SapType = "ZZPALETTE", Description = "Scanned palette number" }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_UNLOAD_PALLATE_VALIDATIONController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_UNLOAD_SAVE_RFC",
                    Route = "api/ZVND_UNLOAD_SAVE_RFC",
                    Group = "Inbound — Unloading",
                    SapRfc = "ZVND_UNLOAD_SAVE_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Save unloading data to SAP. Accepts table of records (PLANT, VEHICLE, EXT_HU, PALETTE, PO_NO, BILL_NO).",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "IM_USER",   SapType = "WWWOBJID",         Description = "Logged-in user" },
                        new RfcParam { Name = "IM_PARMS",  SapType = "ZTT_UNLOAD_SAVE",  Description = "Table: PLANT, VEHICLE, EXT_HU, PALETTE, PO_NO, BILL_NO" }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_UNLOAD_SAVE_RFCController.cs"
                },
                // ── Inbound: Putway to Bin ─────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZVND_PUTWAY_BIN_VAL_RFC",
                    Route = "api/ZVND_PUTWAY_BIN_VAL_RFC",
                    Group = "Inbound — Putway to Bin",
                    SapRfc = "ZVND_PUTWAY_BIN_VAL_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Validate BIN location (LGPLA) for inbound putway. Shows grey on screen after validation.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "IM_USER",  SapType = "WWWOBJID", Description = "Logged-in user" },
                        new RfcParam { Name = "IM_PLANT", SapType = "WERKS_D",  Description = "Plant/DC" },
                        new RfcParam { Name = "IM_BIN",   SapType = "LGPLA",    Description = "Scanned BIN location" }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_PUTWAY_BIN_VAL_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_PUTWAY_PALETTE_VAL_RFC",
                    Route = "api/ZVND_PUTWAY_PALETTE_VAL_RFC",
                    Group = "Inbound — Putway to Bin",
                    SapRfc = "ZVND_PUTWAY_PALETTE_VAL_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Validate Palette for putway. Returns vendor details, PO, invoice, and box count.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "IM_USER",  SapType = "WWWOBJID",  Description = "Logged-in user" },
                        new RfcParam { Name = "IM_PLANT", SapType = "WERKS_D",   Description = "Plant/DC" },
                        new RfcParam { Name = "IM_BIN",   SapType = "ZEXT_HU",   Description = "Validated BIN (HU type)" },
                        new RfcParam { Name = "IM_PALL",  SapType = "ZZPALETTE", Description = "Scanned palette number" }
                    },
                    ResponseTables = new List<string> { "ET_DATA (ZTT_VEN_BOX)", "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_PUTWAY_PALETTE_VAL_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZVND_PUTWAY_SAVE_DATA_RFC",
                    Route = "api/ZVND_PUTWAY_SAVE_DATA_RFC",
                    Group = "Inbound — Putway to Bin",
                    SapRfc = "ZVND_PUTWAY_SAVE_DATA_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Save putway data. IT_DATA table: PLANT, BIN, PALETTE, EXT_HU, PO_NO, BILL_NO.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "IM_USER", SapType = "WWWOBJID",        Description = "Logged-in user" },
                        new RfcParam { Name = "IT_DATA", SapType = "ZTT_PUTWAY_SAVE", Description = "Table: PLANT, BIN, PALETTE, EXT_HU, PO_NO, BILL_NO" }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/Inbound/ZVND_PUTWAY_SAVE_DATA_RFCController.cs"
                },
                // ── HU Creation ─────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZVND_HU_PUSH_API_POST",
                    Route = "api/ZVND_HU_PUSH_API_POST",
                    Group = "HU Creation",
                    SapRfc = "ZVND_HU_PUSH_API_POST",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Push HU data to SAP. Accepts list of HU records.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "HU_NO", SapType = "CHAR" }, new RfcParam { Name = "PO_NO", SapType = "EBELN" },
                        new RfcParam { Name = "ARTICLE_NO", SapType = "MATNR" }, new RfcParam { Name = "DESIGN", SapType = "CHAR" },
                        new RfcParam { Name = "QUANTITY", SapType = "MENGE" }, new RfcParam { Name = "VENDOR_CODE", SapType = "LIFNR" },
                        new RfcParam { Name = "EAN", SapType = "EAN11" }, new RfcParam { Name = "CREATION_DATE", SapType = "DATS" },
                        new RfcParam { Name = "CREATION_TIME", SapType = "TIMS" }, new RfcParam { Name = "STATUS", SapType = "CHAR" }
                    },
                    ResponseTables = new List<string> { "ES_RETURN (BAPIRET2)" },
                    FilePath = "Controllers/HU_Creation/ZVND_HU_PUSH_API_POSTController.cs"
                },
                // ── Finance ─────────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZADVANCE_PAYMENT_RFC",
                    Route = "api/ZADVANCE_PAYMENT_RFC",
                    Group = "Finance",
                    SapRfc = "ZADVANCE_PAYMENT_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Fetch advance payment documents for a company code within a posting date range.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "I_COMPANY_CODE",      SapType = "BUKRS" },
                        new RfcParam { Name = "I_POSTING_DATE_LOW",  SapType = "BUDAT", Description = "Date range start (YYYYMMDD)" },
                        new RfcParam { Name = "I_POSTING_DATE_HIGH", SapType = "BUDAT", Description = "Date range end (YYYYMMDD)" }
                    },
                    ResponseTables = new List<string> { "IT_FINAL", "EX_RETURN (BAPIRET2)" },
                    DataLakeEndpoint = "https://my-dab-app.azurewebsites.net/api/ET_ADVANCE_PAYMENT",
                    FilePath = "Controllers/Finance/ZADVANCE_PAYMENT_RFCController.cs"
                },
                new RfcEndpoint
                {
                    Name = "ZFINANCE_DOCUMENT_RFC",
                    Route = "api/ZFINANCE_DOCUMENT_RFC",
                    Group = "Finance",
                    SapRfc = "ZFINANCE_DOCUMENT_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Finance document retrieval with posting date range filtering.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "I_COMPANY_CODE",      SapType = "BUKRS" },
                        new RfcParam { Name = "I_POSTING_DATE_LOW",  SapType = "DATS" },
                        new RfcParam { Name = "I_POSTING_DATE_HIGH", SapType = "DATS" }
                    },
                    ResponseTables = new List<string> { "EX_RETURN (BAPIRET2)" },
                    DataLakeEndpoint = "https://my-dab-app.azurewebsites.net/api/ET_FINANCE_DOCUMENTS",
                    FilePath = "Controllers/Finance/ZFINANCE_DOCUMENT_RFCController.cs"
                },
                // ── Store Master ────────────────────────────────────────────
                new RfcEndpoint
                {
                    Name = "ZGET_STORE_MASTER_RFC",
                    Route = "api/ZGET_STORE_MASTER_RFC",
                    Group = "Store Master",
                    SapRfc = "ZGET_STORE_MASTER_RFC",
                    Environment = "Dev",
                    SapHost = "192.168.144.174",
                    Description = "Fetch store master data from SAP by store code.",
                    Parameters = new List<RfcParam>
                    {
                        new RfcParam { Name = "IV_STORE_CODE", SapType = "CHAR10", Description = "Store code" }
                    },
                    ResponseTables = new List<string> { "ET_DATA", "EX_RETURN (BAPIRET2)" },
                    DataLakeEndpoint = "https://my-dab-app.azurewebsites.net/api/ET_ZGET_STORE_MASTER",
                    FilePath = "Controllers/Vendor_SRM_Routing/ZGET_STORE_MASTER_RFCController.cs"
                }
            };
        }
    }

    // ── Model classes ────────────────────────────────────────────────────────
    public class RfcEndpoint
    {
        public string Name { get; set; }
        public string Route { get; set; }
        public string Group { get; set; }
        public string SapRfc { get; set; }
        public string Environment { get; set; }
        public string SapHost { get; set; }
        public string Description { get; set; }
        public List<RfcParam> Parameters { get; set; } = new List<RfcParam>();
        public List<string> ResponseTables { get; set; } = new List<string>();
        public string DataLakeEndpoint { get; set; }
        public string FilePath { get; set; }
        public string Method { get; set; } = "POST";
    }

    public class RfcParam
    {
        public string Name { get; set; }
        public string SapType { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; } = true;
    }
}
