-- Migration: BRONZE landing tables for the new /api/po + /api/grn + /api/sales +
-- /api/articles + /api/vendors + /api/plants + /api/article-colors endpoints.
-- Date:      2026-05-28
-- Owner:     Akash (parallel to Shubham's RFC_MASTER pipeline — does NOT touch ET_*)
--
-- Naming:    BRONZE.RFC_API_<resource>
--            Distinct from Shubham's BRONZE.ET_<resource> tables. Zero collision.
--
-- Apply via SnowSight as ACCOUNTADMIN. Idempotent (CREATE IF NOT EXISTS).
--
-- Loaded by: pipeline/api_to_bronze.py via .github/workflows/api-to-bronze-sync.yml
--            Daily 08:00 IST (1hr after Shubham's dead 07:00 cron).

-- ──────────────────────────────────────────────────────────────────────────────
-- Common columns:
--   _LOAD_ID   STRING   uuid of the GHA run that loaded the row
--   _LOAD_TS   TIMESTAMP_NTZ  UTC load time
--   _SOURCE    STRING   "ZMM_PO_DETAILS" / "ZPBI_GRC_DETAILS" / ...
--   _ENV       STRING   "prod" | "qa" | "dev"
--
-- WRITE_MODE:
--   PO, GRN, Sales, Article-Colors → APPEND with (PK + _LOAD_TS) for de-dup at query time
--   Articles, Vendors, Plants      → MERGE/REPLACE — small master tables, full snapshot daily
-- ──────────────────────────────────────────────────────────────────────────────

USE DATABASE V2RETAIL;
USE SCHEMA BRONZE;

-- ── PO (ZMM_PO_DETAILS) ───────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS RFC_API_PO (
  PURCHASING_DOC  STRING,
  PO_TYPE         STRING,
  CREATED_ON      DATE,
  CREATED_BY      STRING,
  SUPPLIER        STRING,
  NET_VALUE       NUMBER(18,2),
  PO_QUANTITY     NUMBER(18,3),
  PLANT           STRING,
  _LOAD_ID        STRING,
  _LOAD_TS        TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
  _SOURCE         STRING,
  _ENV            STRING,
  CONSTRAINT PK_RFC_API_PO PRIMARY KEY (PURCHASING_DOC, _LOAD_TS)
);

-- ── GRN (ZFI_GRC_DETAILS_RFC) ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS RFC_API_GRN (
  MATERIAL_DOC     STRING,
  MAT_DOC_YEAR     STRING,
  MOVEMENT_TYPE    STRING,
  PLANT            STRING,
  SUPPLIER         STRING,
  DEBIT_CREDIT     STRING,
  AMOUNT_IN_LC     NUMBER(18,2),
  QUANTITY         NUMBER(18,3),
  BASE_UNIT        STRING,
  PURCHASE_ORDER   STRING,
  REFERENCE_DOC    STRING,
  SUPPLIER_RECEIVE STRING,
  TRANS_EV_TYPE    STRING,
  POSTING_DATE     DATE,
  ENTERED_ON       DATE,
  TEXT             STRING,
  MOVEMENT_WM      STRING,
  _LOAD_ID         STRING,
  _LOAD_TS         TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
  _SOURCE          STRING,
  _ENV             STRING,
  CONSTRAINT PK_RFC_API_GRN PRIMARY KEY (MATERIAL_DOC, MAT_DOC_YEAR, _LOAD_TS)
);

-- ── Sales (ZPBI_ART_SALES) ────────────────────────────────────────────────────
-- High-volume (~400K rows/day). Append-only with day-key partition.
CREATE TABLE IF NOT EXISTS RFC_API_SALES (
  VBELN       STRING,
  POSNR       STRING,
  FKDAT       DATE,
  WERKS       STRING,
  LGORT       STRING,
  MATNR       STRING,
  FKIMG       NUMBER(18,3),
  VRKME       STRING,
  WAERK       STRING,
  NETWR       NUMBER(18,2),
  KZWI1       NUMBER(18,2),
  KZWI2       NUMBER(18,2),
  MWSBP       NUMBER(18,2),
  WAVWR       NUMBER(18,2),
  NET_VAL     NUMBER(18,2),
  VKP0        NUMBER(18,2),
  KWERT_VPRS  NUMBER(18,2),
  KBETR_VPRS  NUMBER(18,2),
  _LOAD_ID    STRING,
  _LOAD_TS    TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
  _SOURCE     STRING,
  _ENV        STRING
)
CLUSTER BY (FKDAT, WERKS);

-- ── Article Master (ZPBI_ART_MASTER) — full snapshot daily ────────────────────
-- Wide table (75+ fields incl. MVGR_* attributes). Use VARIANT for forward-compat.
CREATE TABLE IF NOT EXISTS RFC_API_ARTICLES (
  MATNR       STRING,
  ERSDA       DATE,
  MTART       STRING,
  MATKL       STRING,
  MEINS       STRING,
  PAYLOAD     VARIANT,                       -- full row JSON, includes MVGR_* attrs
  _LOAD_ID    STRING,
  _LOAD_TS    TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
  _SOURCE     STRING,
  _ENV        STRING,
  CONSTRAINT PK_RFC_API_ARTICLES PRIMARY KEY (MATNR, _LOAD_TS)
);

-- ── Vendor Master (ZPBI_VENDOR_MASTER) ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS RFC_API_VENDORS (
  LIFNR       STRING,
  NAME1       STRING,
  ORT01       STRING,
  REGIO       STRING,
  STRAS       STRING,
  ERDAT       DATE,
  KTOKK       STRING,
  TELF1       STRING,
  SMTP_ADDR   STRING,
  ZTERM       STRING,
  TAXNUM      STRING,
  PAYLOAD     VARIANT,
  _LOAD_ID    STRING,
  _LOAD_TS    TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
  _SOURCE     STRING,
  _ENV        STRING,
  CONSTRAINT PK_RFC_API_VENDORS PRIMARY KEY (LIFNR, _LOAD_TS)
);

-- ── Plant + Storage Location (ZPBI_PLANT_LOCATION) ────────────────────────────
CREATE TABLE IF NOT EXISTS RFC_API_PLANTS (
  ROW_TYPE    STRING,                        -- 'PLANT' or 'LOCATION'
  WERKS       STRING,
  NAME1       STRING,
  LAND1       STRING,
  REGIO       STRING,
  ORT01       STRING,
  PSTLZ       STRING,
  STRAS       STRING,
  LGORT       STRING,                        -- populated when ROW_TYPE='LOCATION'
  PAYLOAD     VARIANT,
  _LOAD_ID    STRING,
  _LOAD_TS    TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
  _SOURCE     STRING,
  _ENV        STRING
);

-- ── Article Color Master (ZPBI_ART_COLOR) ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS RFC_API_ARTICLE_COLORS (
  MATNR       STRING,
  COLOR       STRING,
  ATWTB       STRING,
  _LOAD_ID    STRING,
  _LOAD_TS    TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
  _SOURCE     STRING,
  _ENV        STRING,
  CONSTRAINT PK_RFC_API_ARTICLE_COLORS PRIMARY KEY (MATNR, COLOR, _LOAD_TS)
);

-- ── Sync run log ──────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS RFC_API_SYNC_LOG (
  LOAD_ID      STRING,
  ENDPOINT     STRING,                       -- 'po' | 'grn' | 'sales' | ...
  ENV          STRING,
  DATE_FROM    DATE,
  DATE_TO      DATE,
  STARTED_AT   TIMESTAMP_NTZ,
  ENDED_AT     TIMESTAMP_NTZ,
  ROWS_FROM_API NUMBER,
  ROWS_INSERTED NUMBER,
  STATUS       STRING,                       -- 'OK' | 'ERROR'
  ERROR_TEXT   STRING,
  GITHUB_RUN_URL STRING
);

-- ──────────────────────────────────────────────────────────────────────────────
-- Verify
-- ──────────────────────────────────────────────────────────────────────────────
SELECT TABLE_NAME, ROW_COUNT
FROM V2RETAIL.INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'BRONZE' AND TABLE_NAME LIKE 'RFC_API_%'
ORDER BY TABLE_NAME;
