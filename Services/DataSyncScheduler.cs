using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// Background scheduler for automatic RFC → Snowflake GOLD syncs.
    /// Reads job config from GOLD.RFC_SYNC_JOB. Each active job runs on its own timer.
    ///
    /// Inspired by developer's DataSyncScheduler but uses our RfcSyncExecutionService
    /// instead of HTTP self-calls — more efficient and reliable.
    ///
    /// Started in Global.asax Application_Start. Stopped in Application_End.
    /// Add jobs via POST /api/sync/jobs. No restart required.
    /// </summary>
    public class DataSyncScheduler
    {
        private static readonly Lazy<DataSyncScheduler> _instance =
            new Lazy<DataSyncScheduler>(() => new DataSyncScheduler());
        public static DataSyncScheduler Instance => _instance.Value;

        private readonly ConcurrentDictionary<int, Timer> _timers =
            new ConcurrentDictionary<int, Timer>();
        private readonly SyncJobService          _jobs = new SyncJobService();
        private readonly RfcSyncExecutionService _exec = new RfcSyncExecutionService();
        private readonly object _lock = new object();
        private bool _started = false;

        private DataSyncScheduler() { }

        public void Start()
        {
            if (_started) return;
            _started = true;
            Debug.WriteLine("[DataSyncScheduler] Starting...");
            ReloadAll();
        }

        public void Stop()
        {
            foreach (var kv in _timers) kv.Value?.Dispose();
            _timers.Clear();
            _started = false;
            Debug.WriteLine("[DataSyncScheduler] Stopped.");
        }

        /// <summary>Reload all job timers from Snowflake (call after create/update/delete).</summary>
        public void ReloadAll()
        {
            lock (_lock)
            {
                foreach (var kv in _timers) kv.Value?.Dispose();
                _timers.Clear();
                try
                {
                    var jobs = _jobs.GetAll();
                    int loaded = 0;
                    foreach (var j in jobs)
                    {
                        bool active = Convert.ToBoolean(j.ContainsKey("IS_ACTIVE") ? j["IS_ACTIVE"] : false);
                        if (!active) continue;
                        int id = Convert.ToInt32(j["ID"]);
                        int interval = Convert.ToInt32(j["INTERVAL_MINUTES"]);
                        Schedule(id, interval);
                        loaded++;
                    }
                    Debug.WriteLine($"[DataSyncScheduler] Loaded {loaded} active sync jobs.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[DataSyncScheduler] ReloadAll failed: " + ex.Message);
                }
            }
        }

        public void Schedule(int jobId, int intervalMinutes)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(intervalMinutes, 1));
            var t = new Timer(_ => RunJob(jobId), null, interval, interval);
            _timers[jobId] = t;
        }

        public void Unschedule(int jobId)
        {
            if (_timers.TryRemove(jobId, out var t)) t?.Dispose();
        }

        /// <summary>Run a sync job immediately (used by manual trigger endpoint).</summary>
        public RfcSyncExecutionService.ExecResult RunJobNow(int jobId)
        {
            return RunJob(jobId);
        }

        private RfcSyncExecutionService.ExecResult RunJob(int jobId)
        {
            try
            {
                var job = _jobs.GetById(jobId);
                if (job == null) return null;

                string rfcCode      = job["RFC_CODE"]?.ToString();
                int    windowDays   = Convert.ToInt32(job["DATE_WINDOW_DAYS"] ?? 1);
                int    offsetDays   = Convert.ToInt32(job["DATE_OFFSET_DAYS"] ?? 1);
                string env          = job["ENV"]?.ToString() ?? "PROD";
                string extraJson    = job["EXTRA_PARAMS_JSON"]?.ToString();
                int    intervalMin  = Convert.ToInt32(job["INTERVAL_MINUTES"] ?? 60);

                // Compute date range (yesterday by default: offset=1, window=1)
                var dateTo   = DateTime.Today.AddDays(-offsetDays + windowDays - 1);
                var dateFrom = DateTime.Today.AddDays(-offsetDays);

                // Parse extra params
                Dictionary<string, object> extraParams = null;
                if (!string.IsNullOrWhiteSpace(extraJson))
                    try { extraParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(extraJson); }
                    catch { }

                int logId = 0;
                try { logId = _jobs.LogStart(jobId, rfcCode, dateFrom, dateTo); } catch { }

                Debug.WriteLine($"[Scheduler] Running job {jobId} ({rfcCode}) {dateFrom:yyyy-MM-dd}→{dateTo:yyyy-MM-dd}");
                var result = _exec.Sync(rfcCode, extraParams, dateFrom, dateTo, env);

                try
                {
                    if (logId > 0)
                        _jobs.LogComplete(logId, result.Success, result.FetchedRows, result.WrittenRows,
                            result.ElapsedMs, result.Error);
                    _jobs.UpdateNextRun(jobId, DateTime.UtcNow.AddMinutes(intervalMin));
                }
                catch { }

                Debug.WriteLine($"[Scheduler] Job {jobId} done. Success={result.Success} Written={result.WrittenRows}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scheduler] Job {jobId} CRASHED: {ex.Message}");
                return null;
            }
        }

        public int ActiveJobCount => _timers.Count;
    }
}
