# SAP → Snowflake Pipeline — Architecture + 6-Phase Migration

> **Owner:** Akash + Shubham · **Last updated:** 2026-05-20
> **Repo:** `akash0631/rfc-api` · **Doc lives in master so any session can pick up**

---

## TL;DR

Move from **two parallel SAP-data pipelines** (legacy S28→Snowflake GOLD + v2-sync-engine→Supabase v2srm) to **one pipeline**:

```
SAP → /api/execute/{rfc}/sync → V2RETAIL.BRONZE → SILVER (views) → GOLD (marts) → MART_*
```

Reason: legacy pipelines are unreliable (BRONZE 7-8d stale, sync-engine cron silent since 5/14), they hammer SAP with unbounded scans, and they bypass the medallion. The new path: **1× daily SAP pull, all compute on Snowflake**, SAP guarded against overload by 8 safeguards in the IIS wrapper.

---

## Decisions (locked 2026-05-20)

| Decision | Choice | Rationale |
|---|---|---|
| Cadence | **Daily** SAP→BRONZE pull | Snowflake-side compute can fan-out hourly without re-hitting SAP |
| Real-time (SLT/Datasphere) | **Not pursuing** | Not licensed; not under our control |
| Backfill 2-3yr history | **Defer** | Ship daily first, backfill in Phase 5 |
| 36 EMPTY RFCs | **Param fix on RFC_PARAM directly by Akash** | Bhavesh-side FM bugs unlikely; missing required IM_* params more likely |
| Supabase v2srm | **Keep alive in Phase 1-3**, migrate in Phase 4-5 | 591 tables, used by ARS + SRM web + replenishment + V2_ALLOCATION — too risky to switch at once |
| v2-sync-engine kill date | **End of Phase 5** | Only after Supabase consumers migrated |
| Safeguards before cron | **All 8** mandatory | Wrong-param call must never reach SAP |

---

## Current State (audited 2026-05-20)

### What's working
- Sap-API IIS wrapper at `sap-api.v2retail.net` exposes generic `/api/execute/{rfc}/sync` for all 56 catalog RFCs
- `RFC_MASTER` (52 active) + `RFC_PARAM` (91 rows) catalog drives everything
- Snowflake tasks downstream of BRONZE both running:
  - `TASK_DAILY_GOLD_REFRESH` @ 22:30 UTC (04:00 IST) → `SP_DAILY_GOLD_REFRESH`
  - `TASK_DAILY_MART_REFRESH` @ 00:30 UTC (06:00 IST) → `SP_REFRESH_ANALYTICS_MARTS`
- 31 GOLD MART_* / CUBE_* tables refreshed daily
- **Today's fix (`b6534ac`)**: `BulkInsert` now routes per `RFC_MASTER.TARGET_SCHEMA`. ZMM_VND_PUR verified landing in BRONZE.

### What's broken
| Issue | Evidence |
|---|---|
| Daily auto-sync dead since 2026-05-13 | `RFC_API_ACCESS_LOG`: 937 calls on 5/13, then ≤14/day (manual one-offs) |
| 45 of 52 active RFCs never landed in BRONZE | Only 7 BRONZE tables match `RFC_MASTER.TARGET_TABLE` |
| `ET_SALES_DATA` data 8 d stale | `MAX(SALES_DATE)=2026-05-12` despite `LAST_ALTERED=2026-05-19` |
| `ET_STOCK_DATA` data 7 d stale | 710M rows, max date 2026-05-13 |
| GOLD marts compute on stale BRONZE | "Daily KPI" reports show May-12 reality labeled May-19 |
| Prior dev "copied data once" story | Confirmed: clone_to_bronze.py one-time, no daily delta |

### RFC viability (5/19 Shubham test, BRONZE.RFCTEST_*)

| Bucket | # | RFCs |
|---|---:|---|
| ✅ OK | **8** | RFC_ARTICLE_MASTER, RFC_GRC_DATA_APPLICATION, RFC_Sales_Data, RFC_ZPBI_Vendor_Payment, ZMM_STO_PURN_RFC, ZMM_VND_PUR, RFC_ALL_MOVEMENT, RFC_PLANT_MASTER |
| 🟡 EMPTY (likely param fix) | **36** | RFC_VND_MASTER, RFC_MC_MASTER, RFC_STATE_MASTER, RFC_Country_MASTER, ZFI_PI_DATA_RFC, ZSALES_MOP_RFC, ZRFC_HEADCASHIER, RFC_ARTICLE_COLOUR, ZASSET_CAP, ZFBL1N_PAYMENT_RFC, ZFI_FB65_DISCOUNT_RFC, ZFI_RFC_GSTR1_B2BN, ... |
| 🔴 SAP-side fail | **5** | ZMM_PO_DETAILS (not RFC-enabled), YWM_PRDNEW_RFCIM_DC_WM8 (FU_NOT_FOUND), YWM_PRDNEW_RFC_IM_HU_PND (FU_NOT_FOUND), ZWM_PRD_HUB (FU_NOT_FOUND), SYNC_STORE_PLANT_MASTER (typo) |
| ⏱ Timeout (multi-million rows) | **2** | RFC_STOCK_DATA, ZKSB1_RFC — needs ABAP TOP/LIMIT |
| ⏭ Skip | **1** | ZRFC_ACC_DOC_POST (write RFC) |

---

## Target Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ SAP S/4HANA  (PROD .170:600 / QA .179:600 / DEV .174:210)      │
│ Native RFC over gateway (port 33xx)                             │
└────────────────────────┬────────────────────────────────────────┘
                         │  SAP NCo over RFC
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ .NET IIS wrapper @ sap-api.v2retail.net (V2RfcTest pool)        │
│   /api/execute/{rfc}/sync                                        │
│   Auth: X-RFC-Key: v2-rfc-proxy-2026                            │
│   [8 SAP OVERLOAD SAFEGUARDS — see SAFEGUARDS.md]               │
│   • Mandatory date params  • Max window  • Timeout              │
│   • Concurrency lock       • Rate limit  • Pre-flight params    │
│   • STATUS gate            • Audit log                          │
└────────────────────────┬────────────────────────────────────────┘
                         │  HTTPS REST (JSON)
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ Scheduler: GHA cron .github/workflows/rfc-bronze-sync.yml       │
│   Daily 07:00 IST                                                │
│   matrix = 8 working RFCs                                        │
│   max-parallel = 2  (don't hammer SAP)                          │
│   workflow_dispatch for backfill (date_from/date_to inputs)     │
└────────────────────────┬────────────────────────────────────────┘
                         │  rows append (BulkInsert with auto-create)
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ V2RETAIL.BRONZE  ← raw, append-only, source-faithful           │
│ Owner: ingestion. No business logic, no joins.                  │
└────────────────────────┬────────────────────────────────────────┘
                         │  Snowflake views (zero SAP load)
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ V2RETAIL.SILVER  ← cleaned, typed, no aggregation              │
│ Owner: data engineering. Canonical dims + facts.                │
└────────────────────────┬────────────────────────────────────────┘
                         │  SP_DAILY_GOLD_REFRESH @ 04:00 IST
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ V2RETAIL.GOLD  ← denormalized facts + dims, query-ready        │
│ FACT_*, DIM_*, MART_*, CUBE_*                                    │
└────────────────────────┬────────────────────────────────────────┘
                         │  SP_REFRESH_ANALYTICS_MARTS @ 06:00 IST
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ GOLD.MART_*  ← report-shaped, dashboards + Lovable + Power BI  │
└─────────────────────────────────────────────────────────────────┘
```

### Why daily SAP pull is the right cadence

- SAP RFC calls cost SAP work-process slots. Hourly = 24× the burden.
- Snowflake views on BRONZE refresh **on read** — downstream consumers can pull hourly/realtime from Snowflake at zero SAP cost.
- For genuinely real-time needs (HHT stock check), use existing HHT cache path, not the analytics pipeline.

---

## 6-Phase Migration

### Phase 1 — Safeguards + GHA cron (this week)

Goal: live daily SAP→BRONZE pipeline, SAP bulletproof against bad params.

| Deliverable | Status |
|---|---|
| `docs/SAP-SNOWFLAKE-PIPELINE.md` (this file) | ✅ written |
| `docs/SAFEGUARDS.md` — 8-guard spec | ✅ written |
| `Controllers/RfcSync/RfcExecuteController.cs` — implement 8 safeguards | 🟡 in progress |
| `Models/` — new request validators | 🟡 in progress |
| `migrations/2026-05-20_add_max_window_days.sql` — `ALTER TABLE RFC_MASTER ADD COLUMN MAX_WINDOW_DAYS` | 🟡 in progress |
| `.github/workflows/rfc-bronze-sync.yml` — daily cron | 🟡 in progress |
| Manual test against staging (1 RFC, prove safeguards reject bad inputs) | ⏳ pending |

**Acceptance**: a deliberately wrong call (`{"DateFrom":"1900-01-01","DateTo":"2099-12-31"}`) gets HTTP 400 inside the wrapper without one byte hitting SAP RFC.

### Phase 2 — Fill the 36 EMPTY RFCs (depends on Akash param update)

Goal: BRONZE coverage from 8 RFCs → ~44 RFCs.

| Step | Owner |
|---|---|
| Akash updates `V2RETAIL.GOLD.RFC_PARAM` for the 36 EMPTY RFCs (`IM_BUKRS=1100`, `IM_USERNAME`, `IM_BUDAT_BEGIN`, etc.) | Akash |
| Akash updates `RFC_MASTER.LOOP_SOURCE_QUERY` for PerStore/PerDate iterations | Akash |
| Re-run RFCTEST sweep via `/api/execute/{rfc}?limit=100` | Pipeline (PR #8) |
| Confirm each RFC returns data, then update `RFC_MASTER.STATUS='Active'` | Akash |

Bhavesh items (5 hard fails + 2 timeouts) tracked but **don't block** Phase 2.

### Phase 3 — Dual-run + reconcile (2 weeks after Phase 1 live)

Goal: prove BRONZE→SILVER→GOLD produces same numbers as legacy GOLD direct.

| Step | What |
|---|---|
| 1 | Snowflake job: nightly diff `BRONZE.ET_SALES_DATA` row count + sum(amount) vs legacy `GOLD.ET_SALES_DATA` for same date |
| 2 | Same for ET_STOCK_DATA, ET_GRC_DATA, ET_ARTICLE, ET_PLANT |
| 3 | Telegram alert on divergence > 0.1% |
| 4 | If 14 consecutive days clean → proceed to Phase 4 |

### Phase 4 — Cut legacy Snowflake GOLD writes (week 4)

Goal: v2-sync-engine stops writing to Snowflake GOLD direct.

| Step | Action |
|---|---|
| 1 | Edit `v2-sync-engine` worker, comment out Snowflake-GOLD output target |
| 2 | Keep Supabase v2srm output alive (consumers not yet migrated) |
| 3 | Verify GOLD marts still refresh correctly (now sourcing from BRONZE→SILVER→GOLD only) |

### Phase 5 — Migrate Supabase v2srm consumers (week 5-8)

Goal: ARS, SRM web, replenishment all read from Snowflake instead of v2srm Supabase legacy tables.

| Sub-step | Effort |
|---|---|
| Inventory v2srm consumers + their critical tables | 2 d |
| Build BRONZE → Supabase bridge for high-priority tables (sale_data, po_data_aka, articles, vendors) | 1 wk |
| Cut over ARS to read from Snowflake `V2_ALLOCATION.RAW` (already does for budgets) | 3 d |
| Cut over replenishment worker to BRONZE | 2 d |
| Cut over SRM web app reads | 2 d |
| Stop v2srm legacy table writes one-by-one | rolling |

### Phase 6 — Decom v2-sync-engine + retire S28 (week 9)

Goal: one pipeline, one source of truth.

| Step | Action |
|---|---|
| 1 | Disable v2-sync-engine cron triggers (don't delete worker yet) |
| 2 | Monitor 7 d — confirm nothing complains |
| 3 | Delete v2-sync-engine worker |
| 4 | Stop DataV2 S28 SAP-extract writes (v2-rfc-pipeline cron) |
| 5 | Mark S28 read-only legacy archive |

---

## Safeguards (full spec → `docs/SAFEGUARDS.md`)

| # | Guard | Trigger | Action | Code location |
|---|---|---|---|---|
| 1 | Mandatory date params | `RFC_PARAM.IS_REQUIRED=1` + `DATA_TYPE='Date'` not provided | HTTP 400 | `RfcExecuteController.Sync` pre-flight |
| 2 | Max date window | `DateTo - DateFrom > MAX_WINDOW_DAYS` (new RFC_MASTER col, default 7) | HTTP 400 | `RfcExecuteController.Sync` validation |
| 3 | Server timeout | hits `RFC_MASTER.TIMEOUT_SECONDS` (default 120s) | abort, HTTP 504 | `SapInvoker` timeout |
| 4 | Concurrency lock | sync in flight for same RFC | HTTP 409 | new `GOLD.RFC_SYNC_LOCK` table or in-memory ConcurrentDictionary |
| 5 | Pre-flight param fill | `IS_REQUIRED` missing after `DEFAULT_EXPRESSION` resolve | HTTP 400 | `ApplyParams` pre-check |
| 6 | Rate limit | >10 sync calls/min global | HTTP 429 | `RateLimitFilter` attribute |
| 7 | STATUS gate | `RFC_MASTER.STATUS != 'Active'` | HTTP 403 | `RfcExecuteController.Sync` first check |
| 8 | Audit log | every call | append to `RFC_API_ACCESS_LOG` | already exists ✅ |

---

## RFC catalog config — how to add/fix an RFC

```sql
-- 1. Register RFC
INSERT INTO V2RETAIL.GOLD.RFC_MASTER
  (RFC_CODE, RFC_FUNCTION_NAME, TARGET_SCHEMA, TARGET_TABLE,
   EXECUTION_PATTERN, STATUS, TIMEOUT_SECONDS, MAX_WINDOW_DAYS,
   SCHEDULE_DESC, EXECUTION_TYPE, OWNER, CREATED_BY)
VALUES
  ('MY_NEW_RFC', 'ZMY_FM_NAME', 'BRONZE', 'ET_MY_TABLE',
   'PerDate', 'Active', 120, 7,
   'Daily', 'Scheduled', 'akash', 'akash');

-- 2. Register params
INSERT INTO V2RETAIL.GOLD.RFC_PARAM
  (RFC_ID, PARAM_NAME, PARAM_TYPE, DATA_TYPE, DEFAULT_EXPRESSION, IS_REQUIRED, SORT_ORDER)
SELECT ID, 'IM_DATE_FROM', 'Scalar', 'Date', 'DATE_FROM', 1, 1
FROM V2RETAIL.GOLD.RFC_MASTER WHERE RFC_CODE='MY_NEW_RFC';
-- repeat for IM_DATE_TO, IM_BUKRS, etc.

-- 3. Test with bounded date
-- curl -X POST 'https://sap-api.v2retail.net/api/execute/MY_NEW_RFC/sync' \
--   -H 'X-RFC-Key: v2-rfc-proxy-2026' \
--   -H 'Content-Type: application/json' \
--   -d '{"DateFrom":"2026-05-19","DateTo":"2026-05-19"}'

-- 4. Confirm BRONZE landing
SELECT COUNT(*) FROM V2RETAIL.BRONZE.ET_MY_TABLE;

-- 5. Add to GHA matrix
-- .github/workflows/rfc-bronze-sync.yml → matrix.rfc list
```

---

## Common SAP RFC required params (Akash reference for 36 EMPTY fix)

| Param | Meaning | Typical default |
|---|---|---|
| `IM_BUKRS` | Company code | `1100` (V2 Retail Ltd) |
| `IM_BUKRS_RANGE` | Multi-company | `1100,1200` |
| `IM_USERNAME` | SAP user for context | `POWERBI` |
| `IM_BUDAT_BEGIN` / `IM_BUDAT_END` | Posting date range | yesterday |
| `IM_BLDAT_BEGIN` / `IM_BLDAT_END` | Document date range | yesterday |
| `IM_DATE_FROM` / `IM_DATE_TO` | Generic date range | yesterday |
| `IM_WERKS` / `IM_WERKS_RANGE` | Plant code | from `LOOP_SOURCE_QUERY` |
| `IM_MATNR` / `IM_MATNR_RANGE` | Material | optional, leave blank for all |
| `IM_LIFNR` / `IM_KUNNR` | Vendor / customer | optional |

For `PerStore` RFCs: set `LOOP_SOURCE_QUERY` to `SELECT WERKS FROM V2RETAIL.BRONZE.T001W WHERE WERKS LIKE 'H%'`.

---

## Endpoints / Auth

| Endpoint | Purpose | Auth |
|---|---|---|
| `POST /api/execute/{rfc}` | Returns JSON without writing | `X-RFC-Key: v2-rfc-proxy-2026` |
| `POST /api/execute/{rfc}/sync` | Writes to BRONZE per RFC_MASTER | `X-RFC-Key: v2-rfc-proxy-2026` |
| `POST /api/execute/{rfc}?limit=N` | Sample N rows (PR #8) | same |
| `GET /swagger` | API docs | none |

IIS host: `V2DC-ADDVERB:9292` → `sap-api.v2retail.net` (CF Tunnel)
SAP envs: DEV `192.168.144.174:00:210` / QA `.179:00:600` / PROD `.170:00:600`

---

## Monitoring + alerts

| Signal | Source | Alert |
|---|---|---|
| BRONZE table internal max date older than `MAX_WINDOW_DAYS + 1` | `BRONZE.V_TABLE_FRESHNESS` (TBD view) | Telegram via nubo-wa-bot |
| Sync call fail rate > 10% over 1h | `GOLD.RFC_API_ACCESS_LOG` | Telegram |
| SAP timeout count > 3 in 10 min | same | Page on-call |
| BRONZE vs legacy GOLD divergence > 0.1% | Phase 3 reconcile job | Telegram |

---

## Open items / handover

| Item | Owner | Status |
|---|---|---|
| Akash: update RFC_PARAM for 36 EMPTY RFCs | Akash | 🟡 pending |
| Bhavesh: fix 5 SAP-side hard fails (1 not-RFC-enabled, 3 FU_NOT_FOUND, 1 typo) | Bhavesh | 🔴 |
| Bhavesh: add ABAP TOP/LIMIT to ZKSB1_RFC, RFC_STOCK_DATA | Bhavesh | 🔴 |
| Akash: SnowSight ownership grants on 4 SILVER views (Shubham handover item 6) | Akash | 🔴 |
| Shubham: 2-3yr backfill VBRP/MSEG/EKPO/EKKO (Phase 5) | Shubham (after Phase 4) | 🟡 |
| Akash: drop stale `GOLD.ZMM_VND_PUR` (2158 rows leftover from pre-fix) | Akash (after Phase 3 reconcile) | 🟡 |

---

## Files / repos

- This doc: `akash0631/rfc-api:docs/SAP-SNOWFLAKE-PIPELINE.md`
- Safeguards: `akash0631/rfc-api:docs/SAFEGUARDS.md`
- Cron: `akash0631/rfc-api:.github/workflows/rfc-bronze-sync.yml`
- Controller: `akash0631/rfc-api:Controllers/RfcSync/RfcExecuteController.cs`
- Service: `akash0631/rfc-api:Services/SnowflakeService.cs`
- Migration: `akash0631/rfc-api:migrations/2026-05-20_add_max_window_days.sql`

---

## Change log

| Date | Change | By |
|---|---|---|
| 2026-05-20 | TargetSchema routing fix (`b6534ac`) + deploy-iis Services/** path fix | Akash |
| 2026-05-20 | Doc created. Phase 1 plan + 8 safeguards spec'd | Akash |
| (next) | Safeguards code + cron deployed | — |
