using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vendor_SRM_Routing_Application.Services;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// Caches RFC_MASTER + RFC_PARAM from Snowflake at startup.
    /// Every row in RFC_MASTER becomes a callable API endpoint — no code deploy needed.
    /// Refreshed on demand via /api/catalog/refresh.
    /// </summary>
    public class EndpointRegistryService
    {
        private static readonly Lazy<EndpointRegistryService> _instance =
            new Lazy<EndpointRegistryService>(() => new EndpointRegistryService());
        public static EndpointRegistryService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, RfcEndpoint> _cache =
            new ConcurrentDictionary<string, RfcEndpoint>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly SnowflakeService _sf = new SnowflakeService();
        private readonly object _lock = new object();

        private EndpointRegistryService() { }

        public void Refresh()
        {
            lock (_lock)
            {
                try
                {
                    var rfcs = _sf.QueryAsList(
                        @"SELECT ID, RFC_CODE, RFC_FUNCTION_NAME, DISPLAY_NAME, DESCRIPTION,
                                 DEPARTMENT, SAP_MODULE, TARGET_TABLE, SAP_RETURN_TABLE,
                                 EXECUTION_PATTERN, WRITE_MODE, BULK_BATCH_SIZE,
                                 SAP_CONNECTION_ID, STATUS, SCHEDULE_DESC
                          FROM GOLD.RFC_MASTER
                          WHERE IS_DELETED = FALSE AND STATUS = 'Active'
                          ORDER BY RFC_CODE");

                    _cache.Clear();
                    foreach (var rfc in rfcs)
                    {
                        var code = rfc["RFC_CODE"]?.ToString();
                        if (string.IsNullOrEmpty(code)) continue;
                        var rfcId = Convert.ToInt32(rfc["ID"]);

                        var paramRows = _sf.QueryAsList(
                            "SELECT PARAM_NAME, PARAM_TYPE, DATA_TYPE, DEFAULT_EXPRESSION, IS_REQUIRED, DISPLAY_NAME, SORT_ORDER " +
                            "FROM GOLD.RFC_PARAM WHERE RFC_ID = :id ORDER BY SORT_ORDER",
                            new Dictionary<string, object> { { "id", rfcId } });

                        _cache[code] = new RfcEndpoint
                        {
                            RfcCode       = code,
                            FunctionName  = rfc["RFC_FUNCTION_NAME"]?.ToString(),
                            DisplayName   = rfc["DISPLAY_NAME"]?.ToString(),
                            Description   = rfc["DESCRIPTION"]?.ToString(),
                            Department    = rfc["DEPARTMENT"]?.ToString(),
                            SapModule     = rfc["SAP_MODULE"]?.ToString(),
                            TargetTable   = rfc["TARGET_TABLE"]?.ToString(),
                            ReturnTable   = rfc["SAP_RETURN_TABLE"]?.ToString() ?? "ET_DATA",
                            ExecPattern   = rfc["EXECUTION_PATTERN"]?.ToString() ?? "Single",
                            WriteMode     = rfc["WRITE_MODE"]?.ToString() ?? "Append",
                            BatchSize     = rfc["BULK_BATCH_SIZE"] != null ? Convert.ToInt32(rfc["BULK_BATCH_SIZE"]) : 100000,
                            ConnId        = rfc["SAP_CONNECTION_ID"] != null ? (int?)Convert.ToInt32(rfc["SAP_CONNECTION_ID"]) : 1,
                            ScheduleDesc  = rfc["SCHEDULE_DESC"]?.ToString(),
                            Parameters    = paramRows.Select(p => new RfcParam
                            {
                                Name             = p["PARAM_NAME"]?.ToString(),
                                Type             = p["PARAM_TYPE"]?.ToString(),
                                DataType         = p["DATA_TYPE"]?.ToString(),
                                DefaultExpr      = p["DEFAULT_EXPRESSION"]?.ToString(),
                                IsRequired       = Convert.ToBoolean(p["IS_REQUIRED"] ?? false),
                                DisplayName      = p["DISPLAY_NAME"]?.ToString()
                            }).ToList()
                        };
                    }
                    _lastRefresh = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[EndpointRegistry] Refresh failed: " + ex.Message);
                }
            }
        }

        public RfcEndpoint Get(string rfcCode)
        {
            if (_cache.Count == 0 || DateTime.UtcNow - _lastRefresh > TimeSpan.FromMinutes(30))
                Refresh();
            RfcEndpoint ep;
            return _cache.TryGetValue(rfcCode, out ep) ? ep : null;
        }

        public List<RfcEndpoint> GetAll()
        {
            if (_cache.Count == 0) Refresh();
            return _cache.Values.OrderBy(e => e.RfcCode).ToList();
        }

        public DateTime LastRefresh => _lastRefresh;
    }

    public class RfcEndpoint
    {
        public string RfcCode      { get; set; }
        public string FunctionName { get; set; }
        public string DisplayName  { get; set; }
        public string Description  { get; set; }
        public string Department   { get; set; }
        public string SapModule    { get; set; }
        public string TargetTable  { get; set; }
        public string ReturnTable  { get; set; }
        public string ExecPattern  { get; set; }
        public string WriteMode    { get; set; }
        public int    BatchSize    { get; set; }
        public int?   ConnId       { get; set; }
        public string ScheduleDesc { get; set; }
        public List<RfcParam> Parameters { get; set; } = new List<RfcParam>();
    }

    public class RfcParam
    {
        public string Name        { get; set; }
        public string Type        { get; set; }
        public string DataType    { get; set; }
        public string DefaultExpr { get; set; }
        public bool   IsRequired  { get; set; }
        public string DisplayName { get; set; }
    }
}
