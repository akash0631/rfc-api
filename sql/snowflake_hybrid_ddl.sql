-- ============================================================
-- Hybrid RFC API Platform — Snowflake GOLD DDL
-- Database: V2RETAIL  Schema: GOLD  Warehouse: V2_WH
-- Run once against your Snowflake account.
-- ============================================================

USE DATABASE V2RETAIL;
USE SCHEMA GOLD;

-- ─────────────────────────────────────────────────────────
-- 1. RFC_API_ACCESS_LOG — all API calls logged here
-- ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS GOLD.RFC_API_ACCESS_LOG (
    ID               NUMBER AUTOINCREMENT PRIMARY KEY,
    REQUEST_ID       VARCHAR(100),
    RFC_CODE         VARCHAR(100),
    HTTP_METHOD      VARCHAR(10),
    ENDPOINT         VARCHAR(500),
    REQUEST_BODY     VARCHAR(16777216),
    RESPONSE_STATUS  NUMBER,
    RESPONSE_TIME_MS NUMBER,
    RECORDS_RETURNED NUMBER,
    CLIENT_IP        VARCHAR(50),
    USER_AGENT       VARCHAR(500),
    ERROR_MESSAGE    VARCHAR(4000),
    CREATED_DT       TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP()
);

-- ─────────────────────────────────────────────────────────
-- 2. RFC_SAP_CONNECTION — multi-environment SAP connections
--    Replaces hardcoded BaseController credentials.
--    Seed with your DEV/PROD values (example below).
-- ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS GOLD.RFC_SAP_CONNECTION (
    ID              NUMBER AUTOINCREMENT PRIMARY KEY,
    CONN_NAME       VARCHAR(100) NOT NULL,
    ENV_TYPE        VARCHAR(10)  NOT NULL,   -- PROD | UAT | DEV
    APP_SERVER_HOST VARCHAR(200) NOT NULL,
    SYSTEM_NUMBER   VARCHAR(5)   NOT NULL,
    CLIENT          VARCHAR(5)   NOT NULL,
    SAP_USER        VARCHAR(50)  NOT NULL,
    SAP_PASSWORD    VARCHAR(200) NOT NULL,   -- store encrypted in production
    LANGUAGE        VARCHAR(5)   DEFAULT 'EN',
    IS_ACTIVE       BOOLEAN      DEFAULT TRUE,
    NOTE            VARCHAR(500),
    CREATED_DT      TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP()
);

-- Seed example rows (update with real credentials):
-- INSERT INTO GOLD.RFC_SAP_CONNECTION (CONN_NAME, ENV_TYPE, APP_SERVER_HOST, SYSTEM_NUMBER, CLIENT, SAP_USER, SAP_PASSWORD)
-- VALUES ('SAP_PROD', 'PROD', '192.168.144.170', '00', '600', 'RFC_USER', 'your_password');
-- INSERT INTO GOLD.RFC_SAP_CONNECTION (CONN_NAME, ENV_TYPE, APP_SERVER_HOST, SYSTEM_NUMBER, CLIENT, SAP_USER, SAP_PASSWORD)
-- VALUES ('SAP_DEV',  'DEV',  '192.168.144.174', '00', '210', 'RFC_USER', 'your_password');

-- ─────────────────────────────────────────────────────────
-- 3. RFC_SYNC_JOB — scheduled sync job configuration
--    Replaces CUSTOM_API_SYNC_CONFIG (developer's MSSQL table)
--    with Snowflake GOLD as single source of truth.
-- ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS GOLD.RFC_SYNC_JOB (
    ID               NUMBER AUTOINCREMENT PRIMARY KEY,
    RFC_CODE         VARCHAR(100) NOT NULL,     -- must exist in RFC_MASTER
    DATE_WINDOW_DAYS NUMBER       DEFAULT 1,    -- days to sync per run (1 = 1 day at a time)
    DATE_OFFSET_DAYS NUMBER       DEFAULT 1,    -- start N days ago (1 = yesterday)
    INTERVAL_MINUTES NUMBER       DEFAULT 60,   -- run every N minutes
    ENV              VARCHAR(10)  DEFAULT 'PROD',
    IS_ACTIVE        BOOLEAN      DEFAULT TRUE,
    EXTRA_PARAMS_JSON VARCHAR(4000),            -- JSON: {"I_WERKS": "1000"}
    LAST_RUN_DT      TIMESTAMP_NTZ,
    NEXT_RUN_DT      TIMESTAMP_NTZ,
    CREATED_BY       VARCHAR(100) DEFAULT 'system',
    CREATED_DT       TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
    UPDATED_DT       TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP()
);

-- ─────────────────────────────────────────────────────────
-- 4. RFC_SYNC_LOG — execution history for scheduled jobs
-- ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS GOLD.RFC_SYNC_LOG (
    ID             NUMBER AUTOINCREMENT PRIMARY KEY,
    JOB_ID         NUMBER       NOT NULL,
    RFC_CODE       VARCHAR(100),
    STATUS         VARCHAR(20)  DEFAULT 'Running',  -- Running | Success | Failed
    STARTED_DT     TIMESTAMP_NTZ DEFAULT CURRENT_TIMESTAMP(),
    COMPLETED_DT   TIMESTAMP_NTZ,
    DURATION_SEC   NUMBER,
    ROWS_FETCHED   NUMBER       DEFAULT 0,
    ROWS_WRITTEN   NUMBER       DEFAULT 0,
    DATE_FROM      DATE,
    DATE_TO        DATE,
    ERROR_MESSAGE  VARCHAR(4000),
    TRIGGERED_BY   VARCHAR(50)  DEFAULT 'scheduler'
);

-- ─────────────────────────────────────────────────────────
-- 5. Verify existing RFC_MASTER (should already exist)
-- ─────────────────────────────────────────────────────────
-- SELECT COUNT(*) FROM GOLD.RFC_MASTER;  -- should return 55
-- SELECT COUNT(*) FROM GOLD.RFC_PARAM;   -- should return >100

-- ─────────────────────────────────────────────────────────
-- 6. Quick index for access log query performance
-- ─────────────────────────────────────────────────────────
-- (Snowflake auto-clusters, but explicit clustering keys help for large tables)
-- ALTER TABLE GOLD.RFC_API_ACCESS_LOG CLUSTER BY (RFC_CODE, DATE_TRUNC('day', CREATED_DT));
