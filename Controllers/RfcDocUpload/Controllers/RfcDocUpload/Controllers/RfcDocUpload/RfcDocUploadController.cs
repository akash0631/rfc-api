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
    /// RfcDocUploadController - MVC Controller for uploading RFC documents and generating/pushing controllers.
    /// Routes:
    ///   GET  /RfcDocUpload         -> Index()      Upload form UI
    ///   POST /RfcDocUpload/Upload  -> Upload()     Parse .docx/.txt/.md, generate C# controller code
    ///   POST /RfcDocUpload/Push    -> Push()       Push generated controller to GitHub via API
    ///   GET  /RfcDocUpload/Check   -> CheckFile()  Check if controller already exists in GitHub repo
    /// </summary>
    public class RfcDocUploadController : System.Web.Mvc.Controller
    {
        private const string GH_API_BASE = "https://api.github.com";
        private const string GH_REPO     = "akash0631/rfc-api";
        private const string GH_BRANCH   = "NIKHIL";
        private const string WORKER_URL  = "https://v2-rfc-pipeline.akash-bab.workers.dev";

        // GET /RfcDocUpload
        public ActionResult Index()
        {
            ViewBag.Title    = "V2 Retail - RFC Document Upload";
            ViewBag.GhRepo   = GH_REPO;
            ViewBag.GhBranch = GH_BRANCH;
            return View();
        }

        // POST /RfcDocUpload/Upload
        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file, string folder = "Finance", string env = "dev")
        {
            if (file == null || file.ContentLength == 0)
                return Json(new { success = false, error = "No file uploaded." });

            var allowed = new[] { ".docx", ".txt", ".md" };
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (Array.IndexOf(allowed, ext) < 0)
                return Json(new { success = false, error = "Unsupported file type. Use .docx, .txt, or .md" });

            try
            {
                var bytes = new byte[file.ContentLength];
                file.InputStream.Read(bytes, 0, file.ContentLength);

                string docContent = (ext == ".txt" || ext == ".md")
                    ? Encoding.UTF8.GetString(bytes)
                    : await SendToWorkerForParsing(bytes, file.FileName);

                var rfcName = System.IO.Path.GetFileNameWithoutExtension(file.FileName).ToUpperInvariant();
                var code    = GenerateController(rfcName, docContent, folder, env);
                var path    = "Controllers/" + folder + "/" + rfcName + "Controller.cs";

                return Json(new
                {
                    success       = true,
                    rfcName,
                    folder,
                    filePath      = path,
                    controllerCode = code,
                    description   = ExtractDescription(rfcName, docContent),
                    commitMessage = "feat: " + rfcName + " controller - auto-generated via RFC upload portal"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // GET /RfcDocUpload/Check
        [HttpGet]
        public async Task<ActionResult> CheckFile(string filePath, string token)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(token))
                return Json(new { exists = false, error = "filePath and token required." }, JsonRequestBehavior.AllowGet);

            try
            {
                using (var client = GHClient(token))
                {
                    var r = await client.GetAsync(GH_API_BASE + "/repos/" + GH_REPO + "/contents/" + filePath + "?ref=" + GH_BRANCH);
                    if (r.IsSuccessStatusCode)
                    {
                        var d = JsonConvert.DeserializeObject<JObject>(await r.Content.ReadAsStringAsync());
                        return Json(new { exists = true, sha = d?["sha"]?.ToString() }, JsonRequestBehavior.AllowGet);
                    }
                    return Json(new { exists = false }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { exists = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST /RfcDocUpload/Push
        [HttpPost]
        public async Task<ActionResult> Push(RfcPushRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Token))
                return Json(new { success = false, error = "GitHub token is required." });
            if (string.IsNullOrWhiteSpace(req.FilePath) || string.IsNullOrWhiteSpace(req.Code))
                return Json(new { success = false, error = "FilePath and Code are required." });

            try
            {
                using (var client = GHClient(req.Token))
                {
                    // Check existing SHA
                    string sha = null;
                    var checkR = await client.GetAsync(GH_API_BASE + "/repos/" + GH_REPO + "/contents/" + req.FilePath + "?ref=" + GH_BRANCH);
                    if (checkR.IsSuccessStatusCode)
                    {
                        var ex = JsonConvert.DeserializeObject<JObject>(await checkR.Content.ReadAsStringAsync());
                        sha = ex?["sha"]?.ToString();
                    }

                    var payload = new JObject
                    {
                        ["message"] = req.CommitMessage ?? "feat: " + req.RfcName + " - auto-generated via RFC upload portal",
                        ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(req.Code)),
                        ["branch"]  = GH_BRANCH
                    };
                    if (!string.IsNullOrEmpty(sha)) payload["sha"] = sha;

                    var pushR = await client.PutAsync(
                        GH_API_BASE + "/repos/" + GH_REPO + "/contents/" + req.FilePath,
                        new StringContent(payload.ToString(), Encoding.UTF8, "application/json"));

                    var body = await pushR.Content.ReadAsStringAsync();
                    if (!pushR.IsSuccessStatusCode)
                    {
                        var err = JsonConvert.DeserializeObject<JObject>(body);
                        return Json(new { success = false, error = err?["message"]?.ToString() ?? "GitHub push failed." });
                    }

                    var data      = JsonConvert.DeserializeObject<JObject>(body);
                    var commitSha = data?["commit"]?["sha"]?.ToString() ?? "";
                    var action    = string.IsNullOrEmpty(sha) ? "created" : "updated";

                    return Json(new
                    {
                        success   = true,
                        action,
                        commitSha = commitSha.Length >= 8 ? commitSha.Substring(0, 8) : commitSha,
                        commitUrl = "https://github.com/" + GH_REPO + "/commit/" + commitSha,
                        fileUrl   = "https://github.com/" + GH_REPO + "/blob/" + GH_BRANCH + "/" + req.FilePath,
                        message   = action == "created" ? "Controller created on branch NIKHIL!" : "Controller updated on branch NIKHIL!"
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ---- Helpers ----

        private HttpClient GHClient(string token)
        {
            var c = new HttpClient();
            c.DefaultRequestHeaders.Add("Authorization", "token " + token);
            c.DefaultRequestHeaders.Add("User-Agent", "V2-RFC-Portal/1.0");
            c.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            return c;
        }

        private async Task<string> SendToWorkerForParsing(byte[] bytes, string fileName)
        {
            try
            {
                using (var client = new HttpClient())
                using (var form = new MultipartFormDataContent())
                {
                    var fc = new ByteArrayContent(bytes);
                    fc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    form.Add(fc, "file", fileName);
                    form.Add(new StringContent("parse"), "mode");
                    var r = await client.PostAsync(WORKER_URL + "/parse", form);
                    if (r.IsSuccessStatusCode) return await r.Content.ReadAsStringAsync();
                }
            }
            catch { }
            return string.Empty;
        }

        private string ExtractDescription(string rfcName, string docContent)
        {
            if (!string.IsNullOrWhiteSpace(docContent))
            {
                foreach (var line in docContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var l = line.Trim();
                    if (l.StartsWith("Purpose:", StringComparison.OrdinalIgnoreCase)) return l.Substring(8).Trim();
                    if (l.StartsWith("Description:", StringComparison.OrdinalIgnoreCase)) return l.Substring(12).Trim();
                }
            }
            var n = rfcName.ToUpperInvariant();
            if (n.Contains("DD") && n.Contains("UPD"))    return "Update Delivery Date on PO in SAP";
            if (n.Contains("COST") && n.Contains("UPD"))  return "Update PO Cost in SAP";
            if (n.Contains("QTY") && n.Contains("UPD"))   return "Update PO Quantity in SAP";
            if (n.Contains("ADVANCE"))                    return "Fetch advance payment documents";
            if (n.Contains("PUTWAY"))                     return "Validate and save inbound putway data";
            return rfcName.Replace("_", " ").Replace("RFC", "").Trim() + " via SAP RFC";
        }

        private string GenerateController(string rfcName, string docContent, string folder, string env)
        {
            var ns    = "Vendor_SRM_Routing_Application.Controllers." + folder;
            var envFn = env == "quality" ? "rfcConfigparametersquality"
                      : env == "production" ? "rfcConfigparametersproduction"
                      : "rfcConfigparameters";
            var desc  = ExtractDescription(rfcName, docContent);

            return
@"using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace " + ns + @"
{
    /// <summary>
    /// RFC: " + rfcName + @"
    /// Purpose: " + desc + @"
    /// IMPORT:  IM_DATA - table input (configure fields per RFC spec)
    /// EXPORT:  MSG_TYPE CHAR1   - S=Success, E=Error, W=Warning
    ///          MESSAGE  CHAR100 - Message text
    /// </summary>
    public class " + rfcName + @"Controller : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] " + rfcName + @"Request request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (request == null || request.IM_DATA == null || request.IM_DATA.Count == 0)
                        return Request.CreateResponse(HttpStatusCode.BadRequest,
                            new { Status = false, Message = ""IM_DATA is required."" });

                    RfcConfigParameters rfcPar = BaseController." + envFn + @"();
                    RfcDestination dest        = RfcDestinationManager.GetDestination(rfcPar);
                    IRfcFunction myfun         = dest.Repository.CreateFunction(""" + rfcName + @""");

                    IRfcTable imData = myfun.GetTable(""IM_DATA"");
                    foreach (var row in request.IM_DATA)
                    {
                        imData.Append();
                        foreach (var kv in row)
                            imData.SetValue(kv.Key, kv.Value ?? string.Empty);
                    }

                    myfun.Invoke(dest);

                    string msgType = myfun.GetValue(""MSG_TYPE"")?.ToString() ?? string.Empty;
                    string message = myfun.GetValue(""MESSAGE"")?.ToString()  ?? string.Empty;

                    if (msgType == ""E"")
                        return Request.CreateResponse(HttpStatusCode.BadRequest,
                            new { Status = false, MsgType = msgType, Message = message });

                    return Request.CreateResponse(HttpStatusCode.OK,
                        new { Status = true, MsgType = msgType, Message = message });
                }
                catch (RfcCommunicationException ex)
                { return Request.CreateResponse(HttpStatusCode.ServiceUnavailable, new { Status = false, Message = ""RFC comm error: "" + ex.Message }); }
                catch (RfcLogonException ex)
                { return Request.CreateResponse(HttpStatusCode.Unauthorized, new { Status = false, Message = ""SAP logon failed: "" + ex.Message }); }
                catch (RfcAbapException ex)
                { return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = ""ABAP exception: "" + ex.Message }); }
                catch (Exception ex)
                { return Request.CreateResponse(HttpStatusCode.InternalServerError, new { Status = false, Message = ex.Message }); }
            });
        }
    }

    public class " + rfcName + @"Request
    {
        /// <summary>IM_DATA - table rows as key-value pairs matching RFC table structure</summary>
        public List<Dictionary<string, string>> IM_DATA { get; set; }
    }
}";
        }
    }

    public class RfcPushRequest
    {
        public string Token         { get; set; }
        public string FilePath      { get; set; }
        public string Code          { get; set; }
        public string RfcName       { get; set; }
        public string CommitMessage { get; set; }
    }
}
