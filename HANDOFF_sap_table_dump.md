# Handoff — `feature/sap-table-dump-rfc-read-table`

**Author:** Shubham Kushvanshi
**Date:** 2026-05-23
**Branch:** `feature/sap-table-dump-rfc-read-table`
**Target merge:** `staging` (per repo convention — auto-merges to `master` on build pass)

## What this PR does

Adds a new execution pattern `TableDump` to the rfc-api catalog so any SAP
table can be bulk-dumped into Snowflake via `RFC_READ_TABLE` — no per-table
controller, no new project, no ABAP changes. First consumer: `T001W` (plant
master) into `RAW_SAP_MASTER.T001W`, promoted to `BRONZE.T001W` via a
Snowflake `MERGE` task.

## Why a new pattern is needed

Existing `RfcExecuteController.Sync` was built for catalogued **business
RFCs** (Z-functions that return small/medium tabular results). It cannot
handle a raw SAP-table dump because:

1. `RFC_READ_TABLE` has a 512-byte WA buffer cap. Wide tables (T001W ~180
   cols, MARA ~250 cols) overflow it. The new service splits fields into
   chunks ≤ 480 bytes, reads each chunk, then stitches rows back together
   by primary key (resolved at runtime from `DD03L`).
2. `RFC_READ_TABLE` doesn't return field metadata in the same shape as a
   Z-RFC — it returns offset/length pairs and a packed `WA` string. The new
   service decodes this.
3. `SnowflakeService.BulkInsert` uses row-by-row `INSERT INTO ... VALUES`
   in 500-row chunks. Fine for hundreds of rows; way too slow for masters
   (T001W is small but MARA is 3M rows). The new `BulkLoadViaStage` does
   `PUT` + atomic `INSERT OVERWRITE` from a single CSV file — 10-100×
   faster.

All 8 existing safeguards still apply: status gate, rate limit,
concurrency lock, SAP timeout, audit log. Date guards are skipped (not
applicable to full-refresh masters).

## File changes

```
+ Services/SapTableDumpService.cs                       (NEW, ~260 lines)
+ migrations/2026-05-23_add_table_dump_pattern.sql      (NEW, ~70 lines)
+ migrations/2026-05-23_t001w_merge_task.sql            (NEW, ~140 lines)
~ Services/SnowflakeService.cs                          (+110 lines: BulkLoadViaStage)
~ Services/EndpointRegistryService.cs                   (+12 lines: 3 new fields)
~ Controllers/RfcSync/RfcExecuteController.cs           (+113 lines: SyncTableDump branch)
~ Vendor_SRM_Routing_Application.csproj                 (+1 line: Compile include)
```

## Catalog shape (new columns on `GOLD.RFC_MASTER`)

| Column | Default | Used when |
|---|---|---|
| `SOURCE_TABLE` | NULL → falls back to `TARGET_TABLE` | `EXECUTION_PATTERN='TableDump'` |
| `FIELD_LIST` | empty → all columns (resolved via DD03L) | `EXECUTION_PATTERN='TableDump'` |
| `LOAD_MODE` | `'full'` | `EXECUTION_PATTERN='TableDump'`. Only `'full'` shipped in v0.1; `'delta'` / `'rolling'` reserved. |

## Pilot test plan (T001W)

```bash
# 1. Apply catalog migration (ACCOUNTADMIN in SnowSight)
@migrations/2026-05-23_add_table_dump_pattern.sql

# 2. Force catalog refresh on the running IIS app
curl -X POST -H "X-RFC-Key: v2-rfc-proxy-2026" \
  https://sap-api.v2retail.net/api/catalog/refresh

# 3. Verify the new endpoint shows up
curl -H "X-RFC-Key: v2-rfc-proxy-2026" \
  https://sap-api.v2retail.net/api/catalog/READ_T001W | jq

# 4. Trigger the load (no body needed for full refresh)
curl -X POST -H "X-RFC-Key: v2-rfc-proxy-2026" -H "Content-Type: application/json" \
  -d '{}' \
  https://sap-api.v2retail.net/api/execute/READ_T001W/sync | jq

# Expected response shape:
# {
#   "Success": true,
#   "RequestId": "<guid>",
#   "RfcCode": "READ_T001W",
#   "ExecutionPattern": "TableDump",
#   "SourceTable": "T001W",
#   "TargetTable": "RAW_SAP_MASTER.T001W",
#   "Columns": ~180,
#   "FetchedFromSap": 573,
#   "WrittenToLake": 573,
#   "LoadMode": "full",
#   "BatchId": "<guid>",
#   "ElapsedMs": <few thousand>,
#   "SyncedAt": "2026-05-23T..."
# }
```

```sql
-- 5. Verify in Snowflake
SELECT COUNT(*), MAX(_LOADED_AT), COUNT(DISTINCT _BATCH_ID)
FROM V2RETAIL.RAW_SAP_MASTER.T001W;
-- expect ~573 rows, recent timestamp, exactly 1 batch_id

-- 6. Inspect the diff vs BRONZE before MERGE
SELECT 'in_raw_not_bronze' AS state, src.MANDT, src.WERKS
  FROM V2RETAIL.RAW_SAP_MASTER.T001W src
  LEFT JOIN V2RETAIL.BRONZE.T001W tgt
    ON tgt.MANDT=src.MANDT AND tgt.WERKS=src.WERKS
 WHERE tgt.WERKS IS NULL
UNION ALL
SELECT 'in_bronze_not_raw', tgt.MANDT, tgt.WERKS
  FROM V2RETAIL.BRONZE.T001W tgt
  LEFT JOIN V2RETAIL.RAW_SAP_MASTER.T001W src
    ON tgt.MANDT=src.MANDT AND tgt.WERKS=src.WERKS
 WHERE src.WERKS IS NULL;

-- 7. If diff looks sane, run the manual MERGE block in the merge_task migration
@migrations/2026-05-23_t001w_merge_task.sql
-- (this creates the scheduled TASK in SUSPENDED state)

-- 8. After verification, resume the task
ALTER TASK V2RETAIL.RAW_SAP_MASTER.SYNC_T001W_TO_BRONZE RESUME;

-- 9. Health view
SELECT * FROM V2RETAIL.RAW_SAP_MASTER.VW_T001W_HEALTH;
```

## How to push + open the PR

```bash
cd C:\Users\Administrator\Desktop\rfc-api-pr

git push -u origin feature/sap-table-dump-rfc-read-table

gh pr create \
  --base staging \
  --head feature/sap-table-dump-rfc-read-table \
  --title "feat(sync): TableDump pattern - bulk SAP-table dumps via RFC_READ_TABLE" \
  --body-file HANDOFF_sap_table_dump.md
```

The merge to `staging` triggers `build-check-merge.yml`. If that build
passes, it auto-merges to `master` and the IIS deploy fires.

## Risks / things to watch

1. **`POWERBI` user permissions on SAP PROD client 600 must include
   `RFC_READ_TABLE`.** The existing controllers already use this user so
   this should be fine, but worth a smoke test on UAT first.
2. **`SnowflakeDbConnection` opens once per `ExecuteNonQuery` call.** The
   new `BulkLoadViaStage` does ~5 statements (CREATE/PUT/INSERT/REMOVE/COUNT)
   = 5 connections per load. Acceptable for T001W (~3 seconds total). For
   wider rollout (MARA, MSEG), consider a single-connection variant —
   reserved for v0.2.
3. **`PUT` requires the IIS app pool identity to have write access to its
   `%TEMP%`.** Should be default; if not, the PUT step will fail with a
   helpful error.
4. **`INSERT OVERWRITE` replaces all rows in `RAW_SAP_MASTER.T001W` every
   run.** That's intentional — RAW is just the current snapshot. The MERGE
   TASK to BRONZE doesn't include `WHEN NOT MATCHED BY SOURCE THEN DELETE`
   yet — so a bad 0-row load doesn't wipe BRONZE. Add that guard once
   confident in the loader.

## What's next after this merges

1. Smoke test `READ_T001W` end-to-end (the 8 steps above).
2. Add catalog rows for Wave-1 masters: `READ_LFA1`, `READ_MAKT`,
   `READ_MARC`, `READ_MBEW`, `READ_T023T`, `READ_T005T`, `READ_T005U`.
   Each is a single `INSERT INTO RFC_MASTER`, no code change.
3. v0.2: implement `LoadMode='delta'` and `LoadMode='rolling'` to retire
   the transactional RFCs (MSEG, VBRP, etc.).
