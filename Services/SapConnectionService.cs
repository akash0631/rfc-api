using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SAP.Middleware.Connector;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// Multi-SAP connection service. Reads credentials from Snowflake GOLD.RFC_SAP_CONNECTION.
    /// Enables PROD / UAT / DEV switching per RFC request without code changes.
    ///
    /// Table schema (create with snowflake_hybrid_ddl.sql):
    ///   ID, CONN_NAME, ENV_TYPE (PROD|UAT|DEV), APP_SERVER_HOST, SYSTEM_NUMBER,
    ///   CLIENT, SAP_USER, SAP_PASSWORD, LANGUAGE, IS_ACTIVE
    ///
    /// Falls back gracefully to BaseController static connection if table doesn't exist yet.
    /// </summary>
    public class SapConnectionService
    {
        private static readonly Lazy<SapConnectionService> _instance =
            new Lazy<SapConnectionService>(() => new SapConnectionService());
        public static SapConnectionService Instance => _instance.Value;

        private readonly SnowflakeService _sf = new SnowflakeService();
        private readonly ConcurrentDictionary<string, Dictionary<string, object>> _cache =
            new ConcurrentDictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastRefresh = DateTime.MinValue;
        private bool _tableExists = true;
        private readonly object _lock = new object();

        private SapConnectionService() { }

        private void EnsureLoaded()
        {
            if (!_tableExists) return;
            if (_cache.Count > 0 && DateTime.UtcNow - _lastRefresh < TimeSpan.FromHours(1)) return;
            lock (_lock)
            {
                try
                {
                    var rows = _sf.QueryAsList(
                        "SELECT ID, CONN_NAME, ENV_TYPE, APP_SERVER_HOST, SYSTEM_NUMBER, " +
                        "CLIENT, SAP_USER, SAP_PASSWORD, LANGUAGE " +
                        "FROM GOLD.RFC_SAP_CONNECTION WHERE IS_ACTIVE = TRUE");
                    _cache.Clear();
                    foreach (var row in rows)
                    {
                        var env = row["ENV_TYPE"]?.ToString()?.ToUpper() ?? "PROD";
                        _cache[env] = row;
                        _cache["ID:" + row["ID"]] = row;
                    }
                    _lastRefresh = DateTime.UtcNow;
                }
                catch
                {
                    _tableExists = false; // table doesn't exist yet — fall back mode
                }
            }
        }

        /// <summary>
        /// Returns RfcConfigParameters for the given env (PROD/UAT/DEV) or connection ID.
        /// Returns null if not found (caller should fall back to BaseController static method).
        /// </summary>
        public RfcConfigParameters GetConfig(string env = "PROD", int? connId = null)
        {
            EnsureLoaded();
            Dictionary<string, object> row = null;
            if (connId.HasValue) _cache.TryGetValue("ID:" + connId.Value, out row);
            if (row == null && !string.IsNullOrWhiteSpace(env)) _cache.TryGetValue(env.ToUpper(), out row);
            if (row == null) return null;

            var par = new RfcConfigParameters();
            par.Add(RfcConfigParameters.Name,          row["CONN_NAME"]?.ToString() ?? "SAP_" + env);
            par.Add(RfcConfigParameters.AppServerHost, row["APP_SERVER_HOST"].ToString());
            par.Add(RfcConfigParameters.Client,        row["CLIENT"].ToString());
            par.Add(RfcConfigParameters.SystemNumber,  row["SYSTEM_NUMBER"].ToString());
            par.Add(RfcConfigParameters.User,          row["SAP_USER"].ToString());
            par.Add(RfcConfigParameters.Password,      row["SAP_PASSWORD"].ToString());
            par.Add(RfcConfigParameters.Language,      row["LANGUAGE"]?.ToString() ?? "EN");
            return par;
        }

        public void Invalidate() { _cache.Clear(); _lastRefresh = DateTime.MinValue; _tableExists = true; }
    }
}
