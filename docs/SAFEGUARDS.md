# SAP Overload Safeguards — `/api/execute/{rfc}/sync`

> 8 guards in the IIS wrapper that block bad calls before they reach SAP RFC.
> Goal: even a wrong-param GHA cron or curl typo never overloads SAP work processes.

---

## Threat model

The .NET wrapper at `sap-api.v2retail.net` is the only thing between any HTTP client and the SAP RFC gateway. Without guards:

| Bad input | What happens at SAP without guards | What SAP does |
|---|---|---|
| `DateFrom=1900-01-01, DateTo=2099-12-31` on ET_SALES_DATA | unbounded scan, ~10B rows | hangs work process 5-10 min, blocks other RFC users |
| Missing `IM_BUKRS` on ZFI_PI_DATA | FM returns 0 + ABAP exception trace | logs noise, no data |
| 100 concurrent syncs of RFC_STOCK_DATA | parallel SELECT on MARD/MSEG | SAP dialog steps exhaust, dump SY-SUBRC=8 |
| GHA matrix loop fires 50 RFCs in 30 seconds | gateway connection storm | SAP refuses connections, dispatcher hangs |
| RFC marked `STATUS='Inactive'` but called anyway | runs anyway | wastes SAP slot on dead RFC |

The 8 guards close every one of these.

---

## Guard 1 — Mandatory date params

**What**: Reject any sync call missing date params that `RFC_PARAM` marks `IS_REQUIRED=1` with `DATA_TYPE='Date'`.

**Where**: `RfcExecuteController.Sync` pre-flight, before any SAP call.

**Logic**:
```csharp
var requiredDateParams = ep.Parameters
    .Where(p => p.IsRequired && p.DataType == "Date" && p.Type == "Scalar")
    .ToList();

bool needsDateFrom = requiredDateParams.Any(p => p.DefaultExpr == "DATE_FROM");
bool needsDateTo   = requiredDateParams.Any(p => p.DefaultExpr == "DATE_TO");

if (needsDateFrom && !req.DateFrom.HasValue)
    return BadRequest($"RFC {rfcCode} requires DateFrom (maps to {requiredDateParams.First(p=>p.DefaultExpr=="DATE_FROM").Name})");
if (needsDateTo && !req.DateTo.HasValue)
    return BadRequest($"RFC {rfcCode} requires DateTo");
```

**Response on rejection**:
```json
HTTP 400
{"Success":false,"Error":"RFC ET_SALES_DATA requires DateFrom (maps to IM_DATE_FROM)"}
```

**Test**:
```bash
curl -X POST .../api/execute/RFC_Sales_Data/sync -d '{}'
# expect HTTP 400, body says "requires DateFrom"
```

---

## Guard 2 — Max date window

**What**: Reject if `DateTo - DateFrom > RFC_MASTER.MAX_WINDOW_DAYS` (new column, default 7).

**Where**: `RfcExecuteController.Sync` validation after date params parsed.

**Schema change**:
```sql
-- migrations/2026-05-20_add_max_window_days.sql
ALTER TABLE V2RETAIL.GOLD.RFC_MASTER
  ADD COLUMN MAX_WINDOW_DAYS NUMBER(3,0) DEFAULT 7
  COMMENT 'Hard cap on DateTo - DateFrom for this RFC. Reject wider windows.';

-- Tighter defaults for high-volume RFCs
UPDATE V2RETAIL.GOLD.RFC_MASTER SET MAX_WINDOW_DAYS = 1
  WHERE RFC_CODE IN ('RFC_Sales_Data', 'RFC_STOCK_DATA', 'ET_GOODS_MVT', 'RFC_ALL_MOVEMENT');

UPDATE V2RETAIL.GOLD.RFC_MASTER SET MAX_WINDOW_DAYS = 30
  WHERE RFC_CODE IN ('RFC_ARTICLE_MASTER', 'RFC_PLANT_MASTER', 'RFC_VND_MASTER');
```

**Logic**:
```csharp
if (req.DateFrom.HasValue && req.DateTo.HasValue)
{
    int days = (req.DateTo.Value - req.DateFrom.Value).Days + 1;
    int max = ep.MaxWindowDays > 0 ? ep.MaxWindowDays : 7;
    if (days > max)
        return BadRequest($"DateFrom→DateTo window {days}d exceeds max {max}d for {rfcCode}");
}
```

**Test**: `{"DateFrom":"2026-01-01","DateTo":"2026-05-01"}` → 400 "window 121d exceeds max 1d for RFC_Sales_Data".

---

## Guard 3 — Server timeout

**What**: Enforce `RFC_MASTER.TIMEOUT_SECONDS` (default 120s) on the SAP RFC call. If SAP hasn't returned, abort.

**Where**: `SapInvoker` — wrap `func.Invoke(dest)` in a Task.Run + CancellationToken.

**Logic**:
```csharp
int timeoutSec = ep.TimeoutSeconds > 0 ? ep.TimeoutSeconds : 120;
using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec)))
{
    var invokeTask = Task.Run(() => func.Invoke(dest), cts.Token);
    if (!invokeTask.Wait(TimeSpan.FromSeconds(timeoutSec)))
    {
        cts.Cancel();
        return Request.CreateResponse(
            (HttpStatusCode)504,
            new { Success=false, Error=$"SAP timeout after {timeoutSec}s" });
    }
}
```

**Note**: SAP NCo doesn't honor CancellationToken cleanly — the work process keeps running server-side. We return 504 to client; SAP side will finish on its own clock. This is acceptable: client stops retrying, no GHA matrix runaway.

**Test**: Set RFC_STOCK_DATA `TIMEOUT_SECONDS=30` temporarily; full-plant scan returns 504 in 30s.

---

## Guard 4 — Concurrency lock

**What**: Only one in-flight sync per RFC. Second concurrent call gets HTTP 409.

**Where**: `RfcExecuteController.Sync` entry, using in-memory ConcurrentDictionary (for single-instance IIS) OR `RFC_SYNC_LOCK` table (if scaled).

**Logic** (in-memory, sufficient for V2RfcTest single pool):
```csharp
private static readonly ConcurrentDictionary<string, DateTime> _inFlight = new();

// At start of Sync():
if (!_inFlight.TryAdd(rfcCode, DateTime.UtcNow))
{
    var startedAt = _inFlight[rfcCode];
    return Request.CreateResponse(
        HttpStatusCode.Conflict,
        new { Success=false, Error=$"sync for {rfcCode} already running since {startedAt:o}" });
}
try {
    // ... existing sync logic ...
} finally {
    _inFlight.TryRemove(rfcCode, out _);
}
```

**Test**: Fire 2 syncs of same RFC simultaneously; second returns HTTP 409.

---

## Guard 5 — Pre-flight param fill

**What**: After applying `DEFAULT_EXPRESSION` resolution (DATE_FROM/DATE_TO/TODAY-1), verify EVERY `IS_REQUIRED` param has a value. Reject if any still null.

**Where**: `RfcExecuteController.ApplyParams` — after the existing resolution logic, before SAP call.

**Logic**:
```csharp
var unfilled = ep.Parameters
    .Where(p => p.IsRequired)
    .Where(p => string.IsNullOrEmpty(GetResolvedValue(p, req)))
    .ToList();
if (unfilled.Any())
    return BadRequest($"Missing required params after resolution: {string.Join(", ", unfilled.Select(p => p.Name))}");
```

**Test**: Call RFC_VND_MASTER (assume needs `IM_BUKRS` and no default set) without it → 400 "Missing required params: IM_BUKRS".

---

## Guard 6 — Rate limit

**What**: Global cap of 10 sync calls per minute across all RFCs. Burst protection.

**Where**: Action filter on `Sync` endpoint, using sliding window in memory.

**Logic**:
```csharp
public class SyncRateLimitFilter : ActionFilterAttribute
{
    private static readonly ConcurrentQueue<DateTime> _hits = new();
    private const int MAX_PER_MIN = 10;

    public override void OnActionExecuting(HttpActionContext ctx)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-1);
        while (_hits.TryPeek(out var old) && old < cutoff) _hits.TryDequeue(out _);
        if (_hits.Count >= MAX_PER_MIN)
        {
            ctx.Response = ctx.Request.CreateResponse(
                (HttpStatusCode)429,
                new { Success=false, Error=$"Rate limit: max {MAX_PER_MIN} sync calls/min" });
            return;
        }
        _hits.Enqueue(now);
    }
}
```

**Apply**:
```csharp
[SyncRateLimit]
public HttpResponseMessage Sync(string rfcCode, [FromBody] SyncRequest req) { ... }
```

**Test**: Fire 12 syncs in 30s; calls 11+12 return HTTP 429.

---

## Guard 7 — STATUS gate

**What**: If `RFC_MASTER.STATUS != 'Active'`, return 403. (Already partially exists via the WHERE clause in catalog query — make it explicit.)

**Where**: `RfcExecuteController.Sync` after fetching `ep` from registry.

**Logic**:
```csharp
if (!"Active".Equals(ep.Status, StringComparison.OrdinalIgnoreCase))
    return Request.CreateResponse(
        HttpStatusCode.Forbidden,
        new { Success=false, Error=$"RFC {rfcCode} status={ep.Status} (not Active)" });
```

**Test**: `UPDATE RFC_MASTER SET STATUS='Paused' WHERE RFC_CODE='ZMM_VND_PUR'`, call sync → 403.

---

## Guard 8 — Audit log

**What**: Every call (incl. rejections) logged to `GOLD.RFC_API_ACCESS_LOG` with request body + outcome + elapsed time.

**Status**: ✅ already exists in `_sf.LogAccess(...)` — confirm rejection paths also log.

**Augment**:
```csharp
// At each return-rejection point, add:
_sf.LogAccess(requestId, rfcCode, "/api/execute/" + rfcCode + "/sync",
    statusCode, sw.ElapsedMilliseconds, 0, errorMessage: errMsg);
```

**Query for monitoring**:
```sql
-- Per-RFC daily summary
SELECT
  DATE_TRUNC('day',CREATED_DT)::DATE dt,
  RFC_CODE,
  COUNT(*) calls,
  SUM(CASE WHEN RESPONSE_STATUS=200 THEN 1 ELSE 0 END) ok,
  SUM(CASE WHEN RESPONSE_STATUS=400 THEN 1 ELSE 0 END) bad_request,
  SUM(CASE WHEN RESPONSE_STATUS=409 THEN 1 ELSE 0 END) conflicts,
  SUM(CASE WHEN RESPONSE_STATUS=429 THEN 1 ELSE 0 END) rate_limited,
  SUM(CASE WHEN RESPONSE_STATUS=504 THEN 1 ELSE 0 END) sap_timeouts,
  SUM(CASE WHEN RESPONSE_STATUS=500 THEN 1 ELSE 0 END) errors
FROM V2RETAIL.GOLD.RFC_API_ACCESS_LOG
WHERE ENDPOINT ILIKE '%/sync'
  AND CREATED_DT > DATEADD(day,-7,CURRENT_TIMESTAMP)
GROUP BY 1,2 ORDER BY 1 DESC,2;
```

---

## Test matrix (before merge)

| # | Test | Expected |
|---|---|---|
| 1 | Sync RFC requiring date, body `{}` | HTTP 400 "requires DateFrom" |
| 2 | Sync RFC_Sales_Data, 121-day window | HTTP 400 "window exceeds max" |
| 3 | Sync RFC with hard-stop FM, wait > TIMEOUT_SECONDS | HTTP 504 "SAP timeout" |
| 4 | Fire 2 same-RFC syncs in parallel | second = HTTP 409 |
| 5 | Sync RFC missing required IM_BUKRS, no default | HTTP 400 "Missing required params" |
| 6 | Fire 12 syncs across RFCs in 30s | calls 11,12 = HTTP 429 |
| 7 | Sync RFC with STATUS='Inactive' | HTTP 403 |
| 8 | Any of the above → row appears in RFC_API_ACCESS_LOG with right status | confirmed |
| 9 | Valid sync (ZMM_VND_PUR, 1-day window) | HTTP 200, BRONZE landing, RFC_API_ACCESS_LOG row |

---

## Rollback

If safeguards break legit traffic post-deploy:

```bash
# Revert the safeguard commit
git -C ~/claude/work/rfc-api revert <safeguard-commit-sha>
git push origin staging
# CI auto-merges + deploys
```

`MAX_WINDOW_DAYS` column is additive, no rollback needed (default 7 works for everything).

---

## Owner

- Code: Akash
- Schema migration: Akash (run before deploy)
- Test matrix: Akash + Shubham
- Monitoring queries: in this doc, ready to paste
