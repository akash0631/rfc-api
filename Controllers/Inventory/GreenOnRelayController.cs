using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Inventory
{
    /// <summary>
    /// Green On SSL termination relay for SAP.
    ///
    /// SAP STRUST does not trust GTS Root R4 (Google chain kartmax.in uses).
    /// Rather than block on Basis cert import, SAP posts payload here over
    /// intranet HTTP; this endpoint forwards to Green On over HTTPS
    /// (Windows trust store handles Google chain natively).
    ///
    /// POST /api/inv/greenon-relay
    ///   Body: raw Green On payload (any JSON) — forwarded verbatim
    ///   Headers: X-RFC-Key (auth) — Green On site_token + token injected server-side
    ///
    /// Response mirrors Green On:
    ///   { "http_code": 200, "body": "...", "ok": true, "latency_ms": 234 }
    ///
    /// When SAP STRUST cert is fixed, retire this endpoint — Z_GO_INVENTORY_PUSH
    /// can then call Green On directly.
    /// </summary>
    [RoutePrefix("api/inv")]
    public class GreenOnRelayController : BaseController
    {
        private const string API_KEY = "v2-rfc-proxy-2026";
        private const string GREENON_URL = "https://engine.kartmax.in/api/import/catalogue-import-inventory";
        private const string GREENON_SITE_TOKEN = "UHwgPDz7YxPHimOYNEzg";
        private const string GREENON_API_TOKEN = "UshlJr1FhG3tuXNN4ijf5az2adf7453dfsps";

        [HttpPost]
        [Route("greenon-relay")]
        public async System.Threading.Tasks.Task<IHttpActionResult> Relay()
        {
            IEnumerable<string> keyHeaders;
            bool hasKey = Request.Headers.TryGetValues("X-RFC-Key", out keyHeaders);
            if (!hasKey || keyHeaders.FirstOrDefault() != API_KEY)
            {
                return Json(new
                {
                    ok = false,
                    http_code = 401,
                    body = "Unauthorized — missing or invalid X-RFC-Key"
                });
            }

            string rawBody;
            try
            {
                rawBody = await Request.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    http_code = 400,
                    body = "Failed reading request body: " + ex.Message
                });
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Json(new { ok = false, http_code = 400, body = "Empty body" });
            }

            var started = DateTime.UtcNow;

            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
                {
                    http.DefaultRequestHeaders.Add("site_token", GREENON_SITE_TOKEN);
                    http.DefaultRequestHeaders.Add("token", GREENON_API_TOKEN);

                    var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
                    var resp = await http.PostAsync(GREENON_URL, content);
                    string respBody = await resp.Content.ReadAsStringAsync();
                    int code = (int)resp.StatusCode;
                    bool ok = resp.IsSuccessStatusCode;

                    // Return Green On's actual HTTP code so upstream SAP callers
                    // see failures natively (was returning 200 wrapping ok:false,
                    // which made SAP orchestrators count relay-reached as success).
                    return ResponseMessage(new HttpResponseMessage((System.Net.HttpStatusCode)code)
                    {
                        Content = new StringContent(
                            Newtonsoft.Json.JsonConvert.SerializeObject(new
                            {
                                ok = ok,
                                http_code = code,
                                body = respBody,
                                latency_ms = (int)(DateTime.UtcNow - started).TotalMilliseconds
                            }),
                            Encoding.UTF8, "application/json")
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    ok = false,
                    http_code = 599,
                    body = "Relay exception: " + ex.Message,
                    latency_ms = (int)(DateTime.UtcNow - started).TotalMilliseconds
                });
            }
        }
    }
}
