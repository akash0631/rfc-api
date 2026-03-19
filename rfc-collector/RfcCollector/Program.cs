using System.Linq;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// ── Config from env vars (set in IIS / Azure App Settings) ──────────────────
var iisHost    = Environment.GetEnvironmentVariable("IIS_HOST")   ?? "192.168.151.36";
var iisPort    = Environment.GetEnvironmentVariable("IIS_PORT")   ?? "80";
var sqlServer  = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "192.168.151.46";
var sqlUser    = Environment.GetEnvironmentVariable("SQL_USER")   ?? "sa";
var sqlPass    = Environment.GetEnvironmentVariable("SQL_PASS")   ?? "vrl@55555";
var sqlDb      = Environment.GetEnvironmentVariable("SQL_DB")     ?? "claudetestv2";
var listenPort = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:9090";

builder.WebHost.UseUrls(listenPort);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { 
        Title = "V2 Retail RFC Collector", 
        Version = "v1",
        Description = $"SAP RFC → claudetestv2 @ {sqlServer} | Via IIS: {iisHost}:{iisPort}"
    });
});
builder.Services.AddHttpClient("iis", c => {
    c.BaseAddress = new Uri($"http://{iisHost}:{iisPort}");
    c.Timeout = TimeSpan.FromSeconds(120);
});

// JSON options: return nulls, handle numbers as strings for SAP
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

// ── GET / → health ──────────────────────────────────────────────────────────
app.MapGet("/", () => new {
    status   = "ok",
    version  = "v1",
    iis      = $"http://{iisHost}:{iisPort}",
    sql      = sqlServer,
    database = sqlDb,
    swagger  = "/swagger"
}).WithName("Health");

// ── GET /tables → list tables in claudetestv2 ───────────────────────────────
app.MapGet("/tables", () => {
    using var conn = GetConn();
    using var cmd  = new SqlCommand(
        "SELECT TABLE_NAME, (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_NAME = t.TABLE_NAME) AS col_count FROM INFORMATION_SCHEMA.TABLES t ORDER BY TABLE_NAME", conn);
    using var rdr = cmd.ExecuteReader();
    var tables = new List<object>();
    while (rdr.Read()) tables.Add(new { table = rdr.GetString(0), columns = rdr.GetInt32(1) });
    return tables;
}).WithName("ListTables");

// ── GET /tables/{name}/data → query a table ─────────────────────────────────
app.MapGet("/tables/{name}/data", (string name, int top = 100, string? where = null) => {
    // Basic SQL injection protection
    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9_]+$"))
        return Results.BadRequest(new { error = "Invalid table name" });
    using var conn = GetConn();
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

// ── POST /fetch → call SAP via .36 IIS + store in SQL ──────────────────────
app.MapPost("/fetch", async (FetchRequest req, IHttpClientFactory httpFactory) => {
    // Validate
    if (string.IsNullOrWhiteSpace(req.Rfc))
        return Results.BadRequest(new { error = "rfc is required" });

    var http = httpFactory.CreateClient("iis");

    // Call .36 IIS RFC endpoint
    HttpResponseMessage? iisResp;
    try {
        var body = JsonSerializer.Serialize(req.Params ?? new());
        // Map RFC name → IIS controller route
    // ZADVANCE_PAYMENT_RFC → split on _ → each word Title Case → ZAdvancePaymentRfc → /api/ZAdvancePaymentRfc/Execute
    var parts = req.Rfc.Split('_');
    var pascal = string.Concat(parts.Select(p => p.Length > 0 
        ? char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1).ToLower() : "")
        : ""));
    var controllerRoute = $"/api/{pascal}/Execute";
    iisResp = await http.PostAsync(controllerRoute, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
    } catch (HttpRequestException ex) {
        return Results.Problem($"Cannot reach IIS at {iisHost}:{iisPort} — {ex.Message}", statusCode: 503);
    } catch (TaskCanceledException) {
        return Results.Problem($"IIS timeout (120s) calling /api/{req.Rfc}", statusCode: 504);
    }

    var raw = await iisResp.Content.ReadAsStringAsync();
    if (!iisResp.IsSuccessStatusCode)
        return Results.Problem($"IIS returned HTTP {(int)iisResp.StatusCode}: {raw[..Math.Min(raw.Length,300)]}", statusCode: (int)iisResp.StatusCode);

    using var doc   = JsonDocument.Parse(raw);
    var root        = doc.RootElement;
    var sapStatus   = root.TryGetProperty("Status", out var st)  && st.GetBoolean();
    var sapMessage  = root.TryGetProperty("Message", out var msg) ? msg.GetString() ?? "" : "";

    if (!sapStatus)
        return Results.BadRequest(new { error = "SAP returned failure", sapMessage, raw = raw[..Math.Min(raw.Length, 500)] });

    // Extract table data — look for Data.{TableName} or Data.IT_FINAL etc.
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
                    // Inject params into every row
                    foreach (var p in req.Params ?? new())
                        row[p.Key] = p.Value;
                    row["FETCHED_AT"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    allRows.Add(row);
                }
                break; // take first table
            }
        }
    }

    var rows = allRows.Take(req.MaxRows ?? 5000).ToList();
    if (rows.Count == 0)
        return Results.Ok(new { success = true, fetched = 0, stored = 0, sapMessage, note = "SAP returned 0 rows" });

    // Auto-create table if needed, then insert
    var tableName = req.TargetTable ?? $"ET_{req.Rfc.Replace("_RFC", "")}";
    if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[A-Za-z0-9_]+$"))
        return Results.BadRequest(new { error = "Invalid target table name" });

    int stored = 0;
    var errors = new List<string>();

    using var conn = GetConn(req.TargetDb ?? sqlDb);

    // Auto-create table if it doesn't exist
    var allCols = rows.SelectMany(r => r.Keys).Distinct().ToList();
    var createCols = string.Join(",\n  ", allCols.Select(c => $"[{c}] NVARCHAR(500) NULL"));
    var createSql  = $@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{tableName}')
CREATE TABLE dbo.[{tableName}] (
  [ID] INT IDENTITY(1,1) PRIMARY KEY,
  {createCols}
)";
    using (var cmd = new SqlCommand(createSql, conn)) cmd.ExecuteNonQuery();

    // Auto-add any missing columns
    using (var cmd = new SqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{tableName}'", conn)) {
        using var rdr   = cmd.ExecuteReader();
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
            int i2 = 0;
            foreach (var kv in row) cmd.Parameters.AddWithValue($"@p{i2++}", (object?)kv.Value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            stored++;
        } catch (Exception ex) { errors.Add(ex.Message[..Math.Min(ex.Message.Length, 150)]); }
    }

    return Results.Ok(new {
        success      = true,
        rfc          = req.Rfc,
        table        = tableName,
        fetched      = rows.Count,
        stored,
        totalSap     = allRows.Count,
        truncated    = allRows.Count > rows.Count,
        sapMessage,
        errors       = errors.Count > 0 ? errors.Take(3).ToArray() : null
    });
}).WithName("FetchRfc");

// ── POST /sql → run a SELECT/CREATE/ALTER/DROP ──────────────────────────────
app.MapPost("/sql", (SqlRequest req) => {
    if (string.IsNullOrWhiteSpace(req.Sql)) return Results.BadRequest(new { error = "sql required" });
    var first = req.Sql.TrimStart().Split(' ')[0].ToUpper();
    if (!new[]{"SELECT","CREATE","ALTER","DROP","INSERT","IF"}.Contains(first))
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

// ── Request Models ───────────────────────────────────────────────────────────
record FetchRequest(
    string Rfc,
    Dictionary<string, string>? Params,
    string[]? Columns,
    int? MaxRows,
    string? TargetTable,
    string? TargetDb
);
record SqlRequest(string Sql, string? Database);
