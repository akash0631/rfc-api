using System.Linq;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// ── Config from env vars ─────────────────────────────────────────────────────
var iisHost    = Environment.GetEnvironmentVariable("IIS_HOST")   ?? "sap-api.v2retail.net";
var iisPort    = Environment.GetEnvironmentVariable("IIS_PORT")   ?? "443";
var sqlServer  = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "192.168.151.28";
var sqlUser    = Environment.GetEnvironmentVariable("SQL_USER")   ?? "sa";
var sqlPass    = Environment.GetEnvironmentVariable("SQL_PASS")   ?? "vrl@55555";
var sqlDb      = Environment.GetEnvironmentVariable("SQL_DB")     ?? "DataV2";
var listenPort = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:9090";

// Build IIS base URL — use https for domain names, http for IPs
var iisScheme  = iisHost.StartsWith("192.") || iisHost.StartsWith("10.") ? "http" : "https";
var iisPortStr = (iisScheme == "https" && iisPort == "443") || (iisScheme == "http" && iisPort == "80") ? "" : $":{iisPort}";
var iisBase    = $"{iisScheme}://{iisHost}{iisPortStr}";

builder.WebHost.UseUrls(listenPort);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() {
        Title = "V2 Retail RFC Collector",
        Version = "v1",
        Description = $"SAP RFC → {sqlDb} @ {sqlServer} | Via: {iisBase}"
    });
});
builder.Services.AddHttpClient("iis", c => {
    c.BaseAddress = new Uri(iisBase);
    c.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.ConfigureHttpJsonOptions(o => {
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "RFC Collector v1"); c.RoutePrefix = "swagger"; });

SqlConnection GetConn(string? db = null) {
    var csb = new SqlConnectionStringBuilder {
        DataSource   = $"{sqlServer},1433",
        UserID       = sqlUser,
        Password     = sqlPass,
        InitialCatalog = db ?? sqlDb,
        TrustServerCertificate = true,
        Encrypt      = false,
        ConnectTimeout = 30
    };
    var conn = new SqlConnection(csb.ConnectionString);
    conn.Open();
    return conn;
}

// ── GET / → health ───────────────────────────────────────────────────────────
app.MapGet("/", () => new {
    status   = "ok",
    version  = "v2",
    iis      = iisBase,
    sql      = sqlServer,
    database = sqlDb,
    swagger  = "/swagger"
}).WithName("Health");

// ── GET /tables → list tables ────────────────────────────────────────────────
app.MapGet("/tables", (string? db) => {
    using var conn = GetConn(db);
    using var cmd  = new SqlCommand(
        "SELECT TABLE_NAME, (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_NAME = t.TABLE_NAME AND c.TABLE_SCHEMA = t.TABLE_SCHEMA) AS col_count FROM INFORMATION_SCHEMA.TABLES t WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME", conn);
    using var rdr = cmd.ExecuteReader();
    var tables = new List<object>();
    while (rdr.Read()) tables.Add(new { table = rdr.GetString(0), columns = rdr.GetInt32(1) });
    return tables;
}).WithName("ListTables");

// ── GET /tables/{name}/data → query a table ──────────────────────────────────
app.MapGet("/tables/{name}/data", (string name, int top = 100, string? where = null, string? db = null) => {
    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9_]+$"))
        return Results.BadRequest(new { error = "Invalid table name" });
    using var conn = GetConn(db);
    var sql = $"SELECT TOP {Math.Min(top, 5000)} * FROM dbo.[{name}]";
    if (!string.IsNullOrWhiteSpace(where)) sql += $" WHERE {where}";
    using var cmd = new SqlCommand(sql, conn);
    using var rdr = cmd.ExecuteReader();
    var cols = Enumerable.Range(0, rdr.FieldCount).Select(i => rdr.GetName(i)).ToList();
    var rows = new List<Dictionary<string, object?>>();
    while (rdr.Read()) {
        var row = new Dictionary<string, object?>();
        for (int i = 0; i < rdr.FieldCount; i++)
            row[cols[i]] = rdr.IsDBNull(i) ? null : rdr.GetValue(i)?.ToString();
        rows.Add(row);
    }
    return Results.Ok(new { table = name, count = rows.Count, columns = cols, data = rows });
}).WithName("QueryTable");

// ── POST /fetch → call SAP via sap-api.v2retail.net + store in SQL ───────────
app.MapPost("/fetch", async (FetchRequest req, IHttpClientFactory httpFactory) => {
    if (string.IsNullOrWhiteSpace(req.Rfc))
        return Results.BadRequest(new { error = "rfc is required" });

    var http = httpFactory.CreateClient("iis");

    // Route: try /Post first, fall back to /Execute
    var rfcRoute = req.Route ?? $"api/{req.Rfc}/Post";
    HttpResponseMessage? iisResp;
    try {
        var body = JsonSerializer.Serialize(req.Params ?? new());
        iisResp = await http.PostAsync(rfcRoute, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        // If 404 on /Post, try /Execute
        if (iisResp.StatusCode == System.Net.HttpStatusCode.NotFound && rfcRoute.EndsWith("/Post")) {
            rfcRoute = rfcRoute.Replace("/Post", "/Execute");
            iisResp = await http.PostAsync(rfcRoute, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        }
    } catch (HttpRequestException ex) {
        return Results.Problem($"Cannot reach SAP API at {iisBase} — {ex.Message}", statusCode: 503);
    } catch (TaskCanceledException) {
        return Results.Problem($"SAP API timeout (120s) calling {rfcRoute}", statusCode: 504);
    }

    var raw = await iisResp.Content.ReadAsStringAsync();
    if (!iisResp.IsSuccessStatusCode)
        return Results.Problem($"SAP API returned HTTP {(int)iisResp.StatusCode}: {raw[..Math.Min(raw.Length,300)]}", statusCode: (int)iisResp.StatusCode);

    using var doc   = JsonDocument.Parse(raw);
    var root        = doc.RootElement;

    // Extract table data — look for Data.{TableName} arrays
    var allRows = new List<Dictionary<string, object?>>();
    if (root.TryGetProperty("Data", out var data)) {
        foreach (var prop in data.EnumerateObject()) {
            if (prop.Value.ValueKind == JsonValueKind.Array) {
                foreach (var item in prop.Value.EnumerateArray()) {
                    var row = new Dictionary<string, object?>();
                    if (item.ValueKind == JsonValueKind.Object)
                        foreach (var f in item.EnumerateObject())
                            row[f.Name] = f.Value.ValueKind == JsonValueKind.Null ? null : f.Value.ToString();
                    if (req.Columns?.Length > 0)
                        row = row.Where(kv => req.Columns.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                    foreach (var p in req.Params ?? new())
                        row[p.Key] = p.Value;
                    row["FETCHED_AT"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    allRows.Add(row);
                }
                break;
            }
        }
    }

    var sapStatus  = root.TryGetProperty("Status", out var st) ? (st.ValueKind == JsonValueKind.True ? true : (st.ValueKind == JsonValueKind.False ? false : st.GetString() != "E")) : true;
    var sapMessage = root.TryGetProperty("Message", out var msg) ? msg.GetString() ?? "" : "";

    var rows = allRows.Take(req.MaxRows ?? 50000).ToList();
    if (rows.Count == 0)
        return Results.Ok(new { success = true, fetched = 0, stored = 0, sapStatus, sapMessage, raw = raw[..Math.Min(raw.Length, 300)], note = "No table data in SAP response (may be write RFC)" });

    // Auto-create table + insert
    var tableName = req.TargetTable ?? $"ET_{req.Rfc.Replace("_RFC", "")}";
    if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[A-Za-z0-9_]+$"))
        return Results.BadRequest(new { error = "Invalid target table name" });

    int stored = 0;
    var errors = new List<string>();
    using var conn = GetConn(req.TargetDb ?? sqlDb);

    // Auto-create table if missing
    var allCols    = rows.SelectMany(r => r.Keys).Distinct().ToList();
    var createCols = string.Join(",\n  ", allCols.Select(c => $"[{c}] NVARCHAR(500) NULL"));
    using (var cmd = new SqlCommand($@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{tableName}')
CREATE TABLE dbo.[{tableName}] ([ID] INT IDENTITY(1,1) PRIMARY KEY, {createCols})", conn))
        cmd.ExecuteNonQuery();

    // Auto-add missing columns
    using (var cmd = new SqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{tableName}'", conn)) {
        using var rdr = cmd.ExecuteReader();
        var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (rdr.Read()) existingCols.Add(rdr.GetString(0));
        rdr.Close();
        foreach (var col in allCols.Where(c => !existingCols.Contains(c))) {
            using var alter = new SqlCommand($"ALTER TABLE dbo.[{tableName}] ADD [{col}] NVARCHAR(500) NULL", conn);
            alter.ExecuteNonQuery();
        }
    }

    // Insert rows
    foreach (var row in rows) {
        try {
            var cols  = string.Join(",", row.Keys.Select(c => $"[{c}]"));
            var parms = string.Join(",", row.Keys.Select((c, i) => $"@p{i}"));
            using var cmd = new SqlCommand($"INSERT INTO dbo.[{tableName}] ({cols}) VALUES ({parms})", conn);
            int idx = 0;
            foreach (var kv in row) cmd.Parameters.AddWithValue($"@p{idx++}", (object?)kv.Value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            stored++;
        } catch (Exception ex) { errors.Add(ex.Message[..Math.Min(ex.Message.Length, 150)]); }
    }

    return Results.Ok(new {
        success   = true,
        rfc       = req.Rfc,
        table     = tableName,
        database  = req.TargetDb ?? sqlDb,
        fetched   = rows.Count,
        stored,
        totalSap  = allRows.Count,
        sapMessage,
        errors    = errors.Count > 0 ? errors.Take(3).ToArray() : null
    });
}).WithName("FetchRfc");

// ── POST /sql → execute SQL on DataV2 ───────────────────────────────────────
app.MapPost("/sql", (SqlRequest req) => {
    if (string.IsNullOrWhiteSpace(req.Sql)) return Results.BadRequest(new { error = "sql required" });
    var first = req.Sql.TrimStart().Split(new[]{' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries)[0].ToUpper();
    if (!new[]{"SELECT","CREATE","ALTER","DROP","INSERT","UPDATE","IF","EXEC","TRUNCATE"}.Contains(first))
        return Results.BadRequest(new { error = $"'{first}' not permitted" });
    using var conn = GetConn(req.Database);
    using var cmd  = new SqlCommand(req.Sql, conn) { CommandTimeout = 120 };
    if (first == "SELECT") {
        using var rdr = cmd.ExecuteReader();
        var cols = Enumerable.Range(0, rdr.FieldCount).Select(i => rdr.GetName(i)).ToList();
        var rows = new List<Dictionary<string, object?>>();
        while (rdr.Read()) {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < rdr.FieldCount; i++) row[cols[i]] = rdr.IsDBNull(i) ? null : rdr.GetValue(i)?.ToString();
            rows.Add(row);
        }
        return Results.Ok(new { count = rows.Count, columns = cols, rows });
    }
    cmd.ExecuteNonQuery();
    return Results.Ok(new { success = true });
}).WithName("ExecuteSql");

app.Run();

// ── Request Models ────────────────────────────────────────────────────────────
record FetchRequest(
    string Rfc,
    Dictionary<string, string>? Params,
    string[]? Columns,
    int? MaxRows,
    string? TargetTable,
    string? TargetDb,
    string? Route
);
record SqlRequest(string Sql, string? Database);
