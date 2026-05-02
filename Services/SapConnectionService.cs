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
    /// Actual column names in GOLD.RFC_SAP_CONNECTION:
    ///   ID, CONNECTION_NAME, ENVIRONMENT, APP_SERVER_HOST, SYSTEM_NUMBER,
    ///   CLIENT, RFC_USER, RFC_PASSWORD_ENC, LANGUAGE, IS_ACTIVE
    ///
    /// Current rows: ID=1 PROD 192.168.144.170/600  |  ID=2 UAT 192.168.144.194/210  |  ID=3 DEV 192.168.144.174/210
    /// Falls back gracefully to BaseController static connection if table is unavailable.
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
                    // Column names match the actual GOLD.RFC_SAP_CONNECTION schema
                    var rows = _sf.QueryAsList(
                        "SELECT ID, CONNECTION_NAME, ENVIRONMENT, APP_SERVER_HOST, SYSTEM_NUMBER, " +
                        "CLIENT, RFC_USER, RFC_PASSWORD_ENC, LANGUAGE " +
                        "FROM GOLD.RFC_SAP_CONNECTION WHERE IS_ACTIVE = TRUE");
                    _cache.Clear();
                    foreach (var row in rows)
                    {
                        var env = row["ENVIRONMENT"]?.ToString()?.ToUpper() ?? "PROD";
                        _cache[env] = row;                          // index by PROD/UAT/DEV
                        _cache["ID:" + row["ID"]] = row;            // index by numeric ID
                    }
                    _lastRefresh = DateTime.UtcNow;
                }
                catch
                {
                    _tableExists = false; // graceful fallback — BaseController used instead
                }
            }
        }

        /// <summary>
        /// Returns RfcConfigParameters for the given env (PROD/UAT/DEV) or connection ID.
        /// Returns null if not found — caller falls back to BaseController static method.
        /// </summary>
        public RfcConfigParameters GetConfig(string env = "PROD", int? connId = null)
        {
            EnsureLoaded();
            Dictionary<string, object> row = null;
            if (connId.HasValue) _cache.TryGetValue("ID:" + connId.Value, out row);
            if (row == null && !string.IsNullOrWhiteSpace(env)) _cache.TryGetValue(env.ToUpper(), out row);
            if (row == null) return null;

            var par = new RfcConfigParameters();
            par.Add(RfcConfigParameters.Name,          row["CONNECTION_NAME"]?.ToString() ?? "SAP_" + env);
            par.Add(RfcConfigParameters.AppServerHost, row["APP_SERVER_HOST"].ToString());
            par.Add(RfcConfigParameters.Client,        row["CLIENT"].ToString());
            par.Add(RfcConfigParameters.SystemNumber,  row["SYSTEM_NUMBER"].ToString());
            par.Add(RfcConfigParameters.User,          row["RFC_USER"].ToString());
            par.Add(RfcConfigParameters.Password,      row["RFC_PASSWORD_ENC"].ToString());
            par.Add(RfcConfigParameters.Language,      row["LANGUAGE"]?.ToString() ?? "EN");
            return par;
        }

        public void Invalidate() { _cache.Clear(); _lastRefresh = DateTime.MinValue; _tableExists = true; }
    }
}
