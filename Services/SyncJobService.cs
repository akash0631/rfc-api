using System;
using System.Collections.Generic;
using System.Linq;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// CRUD service for GOLD.RFC_SYNC_JOB — the scheduled sync configuration table.
    /// Replaces developer's CUSTOM_API_SYNC_CONFIG in DataV2/MSSQL with Snowflake GOLD
    /// (single source of truth alongside RFC_MASTER).
    ///
    /// Table created by snowflake_hybrid_ddl.sql.
    /// </summary>
    public class SyncJobService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        public List<Dictionary<string, object>> GetAll()
        {
            return _sf.QueryAsList(@"
                SELECT j.*, l.STATUS AS LAST_STATUS, l.COMPLETED_DT AS LAST_RUN_DT,
                       l.ROWS_WRITTEN AS LAST_ROWS, l.ERROR_MESSAGE AS LAST_ERROR
                FROM GOLD.RFC_SYNC_JOB j
                LEFT JOIN GOLD.RFC_SYNC_LOG l
                  ON l.JOB_ID = j.ID
                  AND l.ID = (SELECT MAX(ID) FROM GOLD.RFC_SYNC_LOG WHERE JOB_ID = j.ID)
                ORDER BY j.RFC_CODE");
        }

        public Dictionary<string, object> GetById(int id)
        {
            var rows = _sf.QueryAsList(
                "SELECT * FROM GOLD.RFC_SYNC_JOB WHERE ID = :id",
                new Dictionary<string, object> { { "id", id } });
            return rows.FirstOrDefault();
        }

        public void Create(SyncJobModel m)
        {
            _sf.ExecuteNonQuery(@"
                INSERT INTO GOLD.RFC_SYNC_JOB
                  (RFC_CODE, DATE_WINDOW_DAYS, DATE_OFFSET_DAYS, INTERVAL_MINUTES, ENV, IS_ACTIVE, EXTRA_PARAMS_JSON)
                VALUES (:rfc, :win, :off, :intv, :env, :active, :extra)",
                new Dictionary<string, object> {
                    { "rfc", m.RfcCode }, { "win", m.DateWindowDays }, { "off", m.DateOffsetDays },
                    { "intv", m.IntervalMinutes }, { "env", m.Env ?? "PROD" },
                    { "active", m.IsActive }, { "extra", m.ExtraParamsJson ?? (object)DBNull.Value }
                });
        }

        public void Update(int id, SyncJobModel m)
        {
            _sf.ExecuteNonQuery(@"
                UPDATE GOLD.RFC_SYNC_JOB SET
                  RFC_CODE=:rfc, DATE_WINDOW_DAYS=:win, DATE_OFFSET_DAYS=:off,
                  INTERVAL_MINUTES=:intv, ENV=:env, IS_ACTIVE=:active,
                  EXTRA_PARAMS_JSON=:extra, UPDATED_DT=CURRENT_TIMESTAMP()
                WHERE ID=:id",
                new Dictionary<string, object> {
                    { "id", id }, { "rfc", m.RfcCode }, { "win", m.DateWindowDays },
                    { "off", m.DateOffsetDays }, { "intv", m.IntervalMinutes },
                    { "env", m.Env ?? "PROD" }, { "active", m.IsActive },
                    { "extra", m.ExtraParamsJson ?? (object)DBNull.Value }
                });
        }

        public void Delete(int id) =>
            _sf.ExecuteNonQuery("DELETE FROM GOLD.RFC_SYNC_JOB WHERE ID = :id",
                new Dictionary<string, object> { { "id", id } });

        public List<Dictionary<string, object>> GetLogs(int jobId, int top = 20) =>
            _sf.QueryAsList(@"
                SELECT * FROM GOLD.RFC_SYNC_LOG
                WHERE JOB_ID = :jid
                ORDER BY STARTED_DT DESC LIMIT :top",
                new Dictionary<string, object> { { "jid", jobId }, { "top", top } });

        public int LogStart(int jobId, string rfcCode, DateTime df, DateTime dt)
        {
            _sf.ExecuteNonQuery(@"
                INSERT INTO GOLD.RFC_SYNC_LOG
                  (JOB_ID, RFC_CODE, STATUS, STARTED_DT, DATE_FROM, DATE_TO)
                VALUES (:jid, :rfc, 'Running', CURRENT_TIMESTAMP(), :df, :dt)",
                new Dictionary<string, object> { { "jid", jobId }, { "rfc", rfcCode },
                    { "df", df.ToString("yyyy-MM-dd") }, { "dt", dt.ToString("yyyy-MM-dd") } });
            var r = _sf.ExecuteScalar("SELECT MAX(ID) FROM GOLD.RFC_SYNC_LOG WHERE JOB_ID = :jid",
                new Dictionary<string, object> { { "jid", jobId } });
            return r == null ? 0 : Convert.ToInt32(r);
        }

        public void LogComplete(int logId, bool success, int fetched, int written, long elapsedMs, string error = null)
        {
            _sf.ExecuteNonQuery(@"
                UPDATE GOLD.RFC_SYNC_LOG SET
                  STATUS=:st, COMPLETED_DT=CURRENT_TIMESTAMP(),
                  DURATION_SEC=:dur, ROWS_FETCHED=:f, ROWS_WRITTEN=:w, ERROR_MESSAGE=:err
                WHERE ID=:id",
                new Dictionary<string, object> {
                    { "id", logId }, { "st", success ? "Success" : "Failed" },
                    { "dur", elapsedMs / 1000 }, { "f", fetched }, { "w", written },
                    { "err", error ?? (object)DBNull.Value }
                });
        }

        public void UpdateNextRun(int jobId, DateTime nextRun) =>
            _sf.ExecuteNonQuery(
                "UPDATE GOLD.RFC_SYNC_JOB SET LAST_RUN_DT=CURRENT_TIMESTAMP(), NEXT_RUN_DT=:next WHERE ID=:id",
                new Dictionary<string, object> { { "id", jobId }, { "next", nextRun.ToString("yyyy-MM-dd HH:mm:ss") } });
    }

    public class SyncJobModel
    {
        public string RfcCode        { get; set; }
        public int    DateWindowDays { get; set; } = 1;
        public int    DateOffsetDays { get; set; } = 1;
        public int    IntervalMinutes { get; set; } = 60;
        public string Env            { get; set; } = "PROD";
        public bool   IsActive       { get; set; } = true;
        public string ExtraParamsJson { get; set; }
    }
}
