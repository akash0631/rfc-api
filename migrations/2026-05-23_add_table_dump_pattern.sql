-- =============================================================================
-- Migration: TableDump execution pattern + RAW_SAP_MASTER landing zone
-- Date: 2026-05-23
-- Author: Shubham Kushvanshi
-- Apply via SnowSight as ACCOUNTADMIN before deploying the matching rfc-api build.
-- Safe to re-run (CREATE/ALTER ... IF NOT EXISTS).
-- =============================================================================

-- 1. New columns on GOLD.RFC_MASTER driving the TableDump branch ---------------
ALTER TABLE V2RETAIL.GOLD.RFC_MASTER
  ADD COLUMN IF NOT EXISTS SOURCE_TABLE VARCHAR
    COMMENT 'SAP table to dump via RFC_READ_TABLE. NULL = use TARGET_TABLE. Only used when EXECUTION_PATTERN=TableDump.';

ALTER TABLE V2RETAIL.GOLD.RFC_MASTER
  ADD COLUMN IF NOT EXISTS FIELD_LIST VARCHAR
    COMMENT 'Comma-separated SAP columns to fetch. NULL/empty = all columns (resolved via DD03L). TableDump only.';

ALTER TABLE V2RETAIL.GOLD.RFC_MASTER
  ADD COLUMN IF NOT EXISTS LOAD_MODE VARCHAR DEFAULT 'full'
    COMMENT 'TableDump load mode: full | delta | rolling. Only ''full'' implemented in v0.1.';

-- 2. Landing schema for raw SAP master data ------------------------------------
CREATE SCHEMA IF NOT EXISTS V2RETAIL.RAW_SAP_MASTER
  COMMENT = 'Raw SAP master-data landing zone. Loaded by rfc-api TableDump endpoints. Promoted to V2RETAIL.BRONZE via Snowflake TASKs.';

-- 3. Audit columns on BRONZE.T001W (idempotent additions; nullable) ------------
ALTER TABLE V2RETAIL.BRONZE.T001W
    ADD COLUMN IF NOT EXISTS _LOADED_AT     TIMESTAMP_LTZ,
    ADD COLUMN IF NOT EXISTS _BATCH_ID      VARCHAR,
    ADD COLUMN IF NOT EXISTS _SOURCE_SYSTEM VARCHAR,
    ADD COLUMN IF NOT EXISTS _BUSINESS_DATE DATE;

-- 4. Catalog entry: READ_T001W ------------------------------------------------
-- Wrapped so the migration is re-runnable (skip if RFC_CODE already exists).
INSERT INTO V2RETAIL.GOLD.RFC_MASTER (
    ID, RFC_CODE, RFC_FUNCTION_NAME, DISPLAY_NAME, DESCRIPTION,
    DEPARTMENT, SAP_MODULE, TARGET_TABLE, TARGET_SCHEMA, SAP_RETURN_TABLE,
    EXECUTION_PATTERN, WRITE_MODE, BULK_BATCH_SIZE, SAP_CONNECTION_ID,
    STATUS, TIMEOUT_SECONDS, MAX_WINDOW_DAYS,
    SOURCE_TABLE, FIELD_LIST, LOAD_MODE
)
SELECT
    COALESCE((SELECT MAX(ID) FROM V2RETAIL.GOLD.RFC_MASTER), 0) + 1,
    'READ_T001W', 'RFC_READ_TABLE', 'Plant Master (T001W) - Full Refresh',
    'Bulk dump of SAP T001W via RFC_READ_TABLE with DD03L PK + chunked WA buffer + stitch. Lands in RAW_SAP_MASTER.T001W; MERGE TASK promotes to BRONZE.T001W.',
    'Data', 'MM', 'T001W', 'RAW_SAP_MASTER', 'DATA',
    'TableDump', 'Overwrite', 0, 1,
    'Active', 180, 1,
    'T001W', '', 'full'
WHERE NOT EXISTS (
    SELECT 1 FROM V2RETAIL.GOLD.RFC_MASTER WHERE RFC_CODE = 'READ_T001W'
);

-- 5. Verify -------------------------------------------------------------------
SELECT RFC_CODE, EXECUTION_PATTERN, SOURCE_TABLE, TARGET_SCHEMA, TARGET_TABLE,
       LOAD_MODE, STATUS, TIMEOUT_SECONDS
FROM V2RETAIL.GOLD.RFC_MASTER
WHERE EXECUTION_PATTERN = 'TableDump'
ORDER BY RFC_CODE;

-- 6. Force catalog refresh on the running IIS app ------------------------------
-- After this migration, hit:
--   POST https://sap-api.v2retail.net/api/catalog/refresh
-- (or wait up to 30 min for the automatic refresh)
