using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_SRM_Routing_Application.Services;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Scheduled Sync Job Management API.
    /// Based on developer's DataSyncApiController but backed by Snowflake GOLD instead of MSSQL.
    ///
    /// Sync jobs define automatic recurring RFC → Snowflake GOLD syncs:
    ///   - Each job maps to an RFC from RFC_MASTER
    ///   - Runs every IntervalMinutes (managed by DataSyncScheduler background timer)
    ///   - Date window: syncs [DateOffsetDays] ago for [DateWindowDays] span
    ///
    /// Create table first: run sql/snowflake_hybrid_ddl.sql against V2RETAIL
    ///
    /// GET  /api/sync/jobs              → list all jobs with last run status
    /// GET  /api/sync/jobs/{id}         → single job detail
    /// POST /api/sync/jobs              → create new sync job
    /// PUT  /api/sync/jobs/{id}         → update job
    /// DELETE /api/sync/jobs/{id}       → delete job
    /// POST /api/sync/jobs/{id}/run     → trigger immediately
    /// GET  /api/sync/jobs/{id}/logs    → execution history (last 20)
    /// POST /api/sync/reload            → force reload all timers from Snowflake
    /// GET  /api/sync/status            → scheduler status
    /// </summary>
    [RoutePrefix("api/sync")]
    public class SyncJobController : BaseController
    {
        private readonly SyncJobService    _svc       = new SyncJobService();
        private readonly DataSyncScheduler _scheduler = DataSyncScheduler.Instance;

        [HttpGet, Route("jobs")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage GetAll()
        {
            try
            {
                var jobs = _svc.GetAll();
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Count = jobs.Count, Jobs = jobs });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        [HttpGet, Route("jobs/{id:int}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Get(int id)
        {
            try
            {
                var job = _svc.GetById(id);
                if (job == null) return Request.CreateResponse(HttpStatusCode.NotFound,
                    new { Success = false, Error = "Job " + id + " not found." });
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Job = job });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        [HttpPost, Route("jobs")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Create([FromBody] SyncJobModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.RfcCode))
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                    new { Success = false, Error = "RfcCode is required." });
            try
            {
                _svc.Create(model);
                _scheduler.ReloadAll();
                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success = true,
                    Message = $"Sync job for '{model.RfcCode}' created. Runs every {model.IntervalMinutes}min."
                });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        [HttpPut, Route("jobs/{id:int}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Update(int id, [FromBody] SyncJobModel model)
        {
            if (model == null) return Request.CreateResponse(HttpStatusCode.BadRequest,
                new { Success = false, Error = "Request body required." });
            try
            {
                _svc.Update(id, model);
                _scheduler.ReloadAll();
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Message = "Job updated." });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        [HttpDelete, Route("jobs/{id:int}")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Delete(int id)
        {
            try
            {
                _svc.Delete(id);
                _scheduler.Unschedule(id);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Message = "Job deleted." });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        /// <summary>Trigger a sync job immediately (outside its normal schedule).</summary>
        [HttpPost, Route("jobs/{id:int}/run")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage RunNow(int id)
        {
            try
            {
                var job = _svc.GetById(id);
                if (job == null) return Request.CreateResponse(HttpStatusCode.NotFound,
                    new { Success = false, Error = "Job " + id + " not found." });

                var result = _scheduler.RunJobNow(id);
                if (result == null) return Error("Job execution returned no result.");

                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success      = result.Success,
                    RfcCode      = result.RfcCode,
                    FetchedRows  = result.FetchedRows,
                    WrittenRows  = result.WrittenRows,
                    TargetTable  = "GOLD." + result.TargetTable,
                    ElapsedMs    = result.ElapsedMs,
                    DateRange    = result.DateRange,
                    Error        = result.Error,
                    QueryApi     = "/api/datalake/" + result.TargetTable
                });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        [HttpGet, Route("jobs/{id:int}/logs")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Logs(int id, int top = 20)
        {
            try
            {
                var logs = _svc.GetLogs(id, top);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Count = logs.Count, Logs = logs });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        /// <summary>Force-reload all scheduler timers from Snowflake (after manual DB changes).</summary>
        [HttpPost, Route("reload")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Reload()
        {
            try
            {
                _scheduler.ReloadAll();
                return Request.CreateResponse(HttpStatusCode.OK, new {
                    Success = true, ActiveJobs = _scheduler.ActiveJobCount,
                    Message = "Scheduler reloaded from GOLD.RFC_SYNC_JOB."
                });
            }
            catch (Exception ex) { return Error(ex.Message); }
        }

        [HttpGet, Route("status")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Status()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new {
                Success    = true,
                ActiveJobs = _scheduler.ActiveJobCount,
                ServerTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        private HttpResponseMessage Error(string msg) =>
            Request.CreateResponse(HttpStatusCode.InternalServerError, new { Success = false, Error = msg });
    }
}
