using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.AbapStudio
{
    /// <summary>
    /// ABAP AI Studio — SAP bridge controller
    /// Connects to SAP Dev (192.168.144.174, Client 210) via SAP NCo
    /// Called by Cloudflare Worker at abap-ai-studio.akash-bab.workers.dev
    /// </summary>
    [RoutePrefix("api/abapstudio")]
    public class AbapStudioController : BaseController
    {
        private const string API_KEY = "abap-studio-sap-2026";

        private bool Authorize()
        {
            IEnumerable<string> values;
            if (Request.Headers.TryGetValues("x-api-key", out values))
            {
                foreach (var v in values)
                    if (v == API_KEY) return true;
            }
            return false;
        }

        // ── Connect / health check ─────────────────────────
        [HttpPost]
        [Route("connect")]
        public IHttpActionResult Connect()
        {
            if (!Authorize()) return Unauthorized();
            try
            {
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("RFC_SYSTEM_INFO");
                fn.Invoke(dest);
                IRfcStructure info = fn.GetStructure("RFCSI_EXPORT");
                string sysid = info.GetString("RFCSYSID");
                string host = info.GetString("RFCHOST");
                return Json(new { connected = true, system_id = sysid, host = host });
            }
            catch (Exception ex)
            {
                return Json(new { connected = false, error = ex.Message });
            }
        }

        // ── Query (Z_RFC_READ_TABLE) ───────────────────────
        [HttpPost]
        [Route("query")]
        public IHttpActionResult Query([FromBody] AbapQueryRequest request)
        {
            if (!Authorize()) return Unauthorized();
            if (request == null || string.IsNullOrEmpty(request.sql))
                return Json(new { error = "SQL query required" });

            try
            {
                // Parse SQL-like query into table + where + max_rows
                string sql = request.sql.Trim();
                string sqlUpper = sql.ToUpper();

                // Extract table name
                var fromMatch = System.Text.RegularExpressions.Regex.Match(sqlUpper, @"FROM\s+""?(\w+)""?");
                if (!fromMatch.Success)
                    return Json(new { error = "Cannot find table name in query" });
                string table = fromMatch.Groups[1].Value;

                // Extract TOP N
                int maxRows = 99999999;
                var topMatch = System.Text.RegularExpressions.Regex.Match(sqlUpper, @"TOP\s+(\d+)");
                if (topMatch.Success) maxRows = int.Parse(topMatch.Groups[1].Value);

                // Extract WHERE clause
                string where = "";
                var whereMatch = System.Text.RegularExpressions.Regex.Match(sqlUpper, @"WHERE\s+(.+?)(?:\s+ORDER\s+BY|\s+GROUP\s+BY|$)", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (whereMatch.Success) where = whereMatch.Groups[1].Value.Trim();

                // Call Z_RFC_READ_TABLE
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_RFC_READ_TABLE");
                fn.SetValue("IV_TABLE", table);
                fn.SetValue("IV_WHERE", where);
                fn.SetValue("IV_ROWCOUNT", maxRows);
                fn.Invoke(dest);

                string evResult = fn.GetString("EV_RESULT");
                string evFields = fn.GetString("EV_FIELDS");
                string evError = fn.GetString("EV_ERROR");

                if (!string.IsNullOrEmpty(evError))
                    return Json(new { error = "SAP Error: " + evError });

                var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
                    string.IsNullOrEmpty(evResult) ? "[]" : evResult);
                var fields = new List<string>();
                if (!string.IsNullOrEmpty(evFields))
                {
                    foreach (var f in evFields.Split('|'))
                    {
                        var trimmed = f.Trim();
                        if (!string.IsNullOrEmpty(trimmed)) fields.Add(trimmed);
                    }
                }

                return Json(new { columns = fields, rows = rows, row_count = rows.Count });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // ── Source code viewer (Z_GET_REPORT_SOURCE) ───────
        [HttpPost]
        [Route("source")]
        public IHttpActionResult Source([FromBody] AbapSourceRequest request)
        {
            if (!Authorize()) return Unauthorized();
            if (request == null || string.IsNullOrEmpty(request.program))
                return Json(new { error = "Program name required" });

            try
            {
                string prog = request.program.Trim().ToUpper();
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_GET_REPORT_SOURCE");
                fn.SetValue("IV_PROGRAM", prog);
                fn.Invoke(dest);

                string source = fn.GetString("EV_SOURCE");
                if (string.IsNullOrEmpty(source))
                    return Json(new { error = "Program " + prog + " not found or no authorization" });

                return Json(new { program = prog, source = source, lines = source.Split('\n').Length });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // ── Tables listing ─────────────────────────────────
        [HttpPost]
        [Route("tables")]
        public IHttpActionResult Tables([FromBody] AbapTablesRequest request)
        {
            if (!Authorize()) return Unauthorized();
            try
            {
                string where = "TABCLASS = 'TRANSP'";
                if (request != null && !string.IsNullOrEmpty(request.prefix))
                    where += " AND TABNAME LIKE '" + request.prefix.ToUpper() + "%'";

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_RFC_READ_TABLE");
                fn.SetValue("IV_TABLE", "DD02L");
                fn.SetValue("IV_WHERE", where);
                fn.SetValue("IV_ROWCOUNT", 200);
                fn.Invoke(dest);

                string evResult = fn.GetString("EV_RESULT");
                var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
                    string.IsNullOrEmpty(evResult) ? "[]" : evResult);

                return Json(new { tables = rows, count = rows.Count });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // ── Describe table fields ──────────────────────────
        [HttpPost]
        [Route("describe")]
        public IHttpActionResult Describe([FromBody] AbapDescribeRequest request)
        {
            if (!Authorize()) return Unauthorized();
            if (request == null || string.IsNullOrEmpty(request.table))
                return Json(new { error = "Table name required" });

            try
            {
                string where = "TABNAME = '" + request.table.ToUpper() + "'";
                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_RFC_READ_TABLE");
                fn.SetValue("IV_TABLE", "DD03L");
                fn.SetValue("IV_WHERE", where);
                fn.SetValue("IV_ROWCOUNT", 200);
                fn.Invoke(dest);

                string evResult = fn.GetString("EV_RESULT");
                var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
                    string.IsNullOrEmpty(evResult) ? "[]" : evResult);

                return Json(new { columns = rows, count = rows.Count });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // ── Programs listing ───────────────────────────────
        [HttpPost]
        [Route("programs")]
        public IHttpActionResult Programs([FromBody] AbapTablesRequest request)
        {
            if (!Authorize()) return Unauthorized();
            try
            {
                string prefix = (request != null && !string.IsNullOrEmpty(request.prefix)) ? request.prefix.ToUpper() : "Z";
                string where = "NAME LIKE '" + prefix + "%'";

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction fn = dest.Repository.CreateFunction("Z_RFC_READ_TABLE");
                fn.SetValue("IV_TABLE", "TRDIR");
                fn.SetValue("IV_WHERE", where);
                fn.SetValue("IV_ROWCOUNT", 50);
                fn.Invoke(dest);

                string evResult = fn.GetString("EV_RESULT");
                var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
                    string.IsNullOrEmpty(evResult) ? "[]" : evResult);

                return Json(new { programs = rows, count = rows.Count });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

    // ── Deploy (Z_UPLOAD_PROGRAM) ──────────────────────
    [HttpPost]
    [Route("deploy")]
    public IHttpActionResult Deploy([FromBody] AbapDeployRequest request)
    {
        if (!Authorize()) return Unauthorized();
        if (request == null || string.IsNullOrEmpty(request.program) || string.IsNullOrEmpty(request.source))
            return Json(new { error = "Program name and source code required" });

        try
        {
            RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction fn = dest.Repository.CreateFunction("Z_UPLOAD_PROGRAM");
            fn.SetValue("IV_PROGRAM", request.program.Trim().ToUpper());
            fn.SetValue("IV_SOURCE", request.source);
            fn.SetValue("IV_TITLE", request.title ?? "AI Generated Program");
            fn.SetValue("IV_PROGRAM_TYPE", request.program_type ?? "1");
            fn.SetValue("IV_TRANSPORT", request.transport ?? "");
            fn.SetValue("IV_OVERWRITE", request.overwrite ?? "X");
            fn.Invoke(dest);

            string status = fn.GetString("EV_STATUS");
            string message = fn.GetString("EV_MESSAGE");
            string program = fn.GetString("EV_PROGRAM");
            string transport = fn.GetString("EV_TRANSPORT");

            return Json(new { status, message, program, transport });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    // ── Test (Z_RUN_UNIT_TEST) ─────────────────────────
    [HttpPost]
    [Route("test")]
    public IHttpActionResult RunTest([FromBody] AbapTestRequest request)
    {
        if (!Authorize()) return Unauthorized();
        if (request == null || string.IsNullOrEmpty(request.program))
            return Json(new { error = "Program name required" });

        try
        {
            RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction fn = dest.Repository.CreateFunction("Z_RUN_UNIT_TEST");
            fn.SetValue("IV_PROGRAM", request.program.Trim().ToUpper());
            fn.SetValue("IV_TEST_CLASS", request.test_class ?? "");
            fn.Invoke(dest);

            string status = fn.GetString("EV_STATUS");
            string result = fn.GetString("EV_RESULT");
            string summary = fn.GetString("EV_SUMMARY");
            int total = fn.GetInt("EV_TOTAL");
            int passed = fn.GetInt("EV_PASSED");
            int failed = fn.GetInt("EV_FAILED");

            return Json(new { status, result, summary, total, passed, failed });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    }

    // ── Request models ─────────────────────────────────────
    public class AbapQueryRequest
    {
        public string sql { get; set; }
    }

    public class AbapSourceRequest
    {
        public string program { get; set; }
    }

    public class AbapTablesRequest
    {
        public string prefix { get; set; }
    }


    public class AbapDeployRequest
    {
        public string program { get; set; }
        public string source { get; set; }
        public string title { get; set; }
        public string program_type { get; set; }
        public string transport { get; set; }
        public string overwrite { get; set; }
    }

    public class AbapTestRequest
    {
        public string program { get; set; }
        public string test_class { get; set; }
    }

    public class AbapDescribeRequest
    {
        public string table { get; set; }
    }
}
