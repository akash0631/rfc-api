-- =============================================================================
-- Pair of the TableDump migration: promotes RAW_SAP_MASTER.T001W -> BRONZE.T001W
-- Date: 2026-05-23
-- Run AFTER the rfc-api TableDump endpoint has done its first successful load.
-- =============================================================================

USE DATABASE V2RETAIL;

-- ---------------------------------------------------------------------------
-- 1. Manual one-shot MERGE - run first to verify the diff looks sane
-- ---------------------------------------------------------------------------
MERGE INTO V2RETAIL.BRONZE.T001W AS tgt
USING V2RETAIL.RAW_SAP_MASTER.T001W AS src
   ON tgt.MANDT = src.MANDT
  AND tgt.WERKS = src.WERKS
WHEN MATCHED THEN UPDATE SET
    tgt.NAME1 = src.NAME1, tgt.BWKEY = src.BWKEY, tgt.KUNNR = src.KUNNR,
    tgt.LIFNR = src.LIFNR, tgt.FABKL = src.FABKL, tgt.NAME2 = src.NAME2,
    tgt.STRAS = src.STRAS, tgt.PFACH = src.PFACH, tgt.PSTLZ = src.PSTLZ,
    tgt.ORT01 = src.ORT01, tgt.EKORG = src.EKORG, tgt.VKORG = src.VKORG,
    tgt.CHAZV = src.CHAZV, tgt.KKOWK = src.KKOWK, tgt.KORDB = src.KORDB,
    tgt.BEDPL = src.BEDPL, tgt.LAND1 = src.LAND1, tgt.REGIO = src.REGIO,
    tgt.COUNC = src.COUNC, tgt.CITYC = src.CITYC, tgt.ADRNR = src.ADRNR,
    tgt.IWERK = src.IWERK, tgt.TXJCD = src.TXJCD, tgt.VTWEG = src.VTWEG,
    tgt.SPART = src.SPART, tgt.SPRAS = src.SPRAS, tgt.WKSOP = src.WKSOP,
    tgt.AWSLS = src.AWSLS, tgt.CHAZV_OLD = src.CHAZV_OLD, tgt.VLFKZ = src.VLFKZ,
    tgt.BZIRK = src.BZIRK, tgt.ZONE1 = src.ZONE1, tgt.TAXIW = src.TAXIW,
    tgt.BZQHL = src.BZQHL, tgt.LET01 = src.LET01, tgt.LET02 = src.LET02,
    tgt.LET03 = src.LET03, tgt.TXNAM_MA1 = src.TXNAM_MA1, tgt.TXNAM_MA2 = src.TXNAM_MA2,
    tgt.TXNAM_MA3 = src.TXNAM_MA3, tgt.BETOL = src.BETOL, tgt.J_1BBRANCH = src.J_1BBRANCH,
    tgt.VTBFI = src.VTBFI, tgt.FPRFW = src.FPRFW, tgt.ACHVM = src.ACHVM,
    tgt.DVSART = src.DVSART, tgt.NODETYPE = src.NODETYPE, tgt.NSCHEMA = src.NSCHEMA,
    tgt.PKOSA = src.PKOSA, tgt.MISCH = src.MISCH, tgt.MGVUPD = src.MGVUPD,
    tgt.VSTEL = src.VSTEL, tgt.MGVLAUPD = src.MGVLAUPD, tgt.MGVLAREVAL = src.MGVLAREVAL,
    tgt.SOURCING = src.SOURCING, tgt.NO_DEFAULT_BATCH_MANAGEMENT = src.NO_DEFAULT_BATCH_MANAGEMENT,
    tgt.FSH_MG_ARUN_REQ = src.FSH_MG_ARUN_REQ, tgt.FSH_SEAIM = src.FSH_SEAIM,
    tgt.FSH_BOM_MAINTENANCE = src.FSH_BOM_MAINTENANCE, tgt.FSH_GROUP_PR = src.FSH_GROUP_PR,
    tgt.ARUN_FIX_BATCH = src.ARUN_FIX_BATCH, tgt.OILIVAL = src.OILIVAL,
    tgt.OIHVTYPE = src.OIHVTYPE, tgt.OIHCREDIPI = src.OIHCREDIPI,
    tgt.STORETYPE = src.STORETYPE, tgt.DEP_STORE = src.DEP_STORE,
    tgt.EXTRACT_DATE = src._LOADED_AT::DATE,
    tgt._LOADED_AT = src._LOADED_AT, tgt._BATCH_ID = src._BATCH_ID,
    tgt._SOURCE_SYSTEM = src._SOURCE_SYSTEM, tgt._BUSINESS_DATE = src._BUSINESS_DATE
WHEN NOT MATCHED THEN INSERT (
    MANDT, WERKS, NAME1, BWKEY, KUNNR, LIFNR, FABKL, NAME2, STRAS, PFACH, PSTLZ,
    ORT01, EKORG, VKORG, CHAZV, KKOWK, KORDB, BEDPL, LAND1, REGIO, COUNC, CITYC,
    ADRNR, IWERK, TXJCD, VTWEG, SPART, SPRAS, WKSOP, AWSLS, CHAZV_OLD, VLFKZ,
    BZIRK, ZONE1, TAXIW, BZQHL, LET01, LET02, LET03, TXNAM_MA1, TXNAM_MA2,
    TXNAM_MA3, BETOL, J_1BBRANCH, VTBFI, FPRFW, ACHVM, DVSART, NODETYPE,
    NSCHEMA, PKOSA, MISCH, MGVUPD, VSTEL, MGVLAUPD, MGVLAREVAL, SOURCING,
    NO_DEFAULT_BATCH_MANAGEMENT, FSH_MG_ARUN_REQ, FSH_SEAIM, FSH_BOM_MAINTENANCE,
    FSH_GROUP_PR, ARUN_FIX_BATCH, OILIVAL, OIHVTYPE, OIHCREDIPI, STORETYPE,
    DEP_STORE, EXTRACT_DATE, _LOADED_AT, _BATCH_ID, _SOURCE_SYSTEM, _BUSINESS_DATE
) VALUES (
    src.MANDT, src.WERKS, src.NAME1, src.BWKEY, src.KUNNR, src.LIFNR, src.FABKL,
    src.NAME2, src.STRAS, src.PFACH, src.PSTLZ, src.ORT01, src.EKORG, src.VKORG,
    src.CHAZV, src.KKOWK, src.KORDB, src.BEDPL, src.LAND1, src.REGIO, src.COUNC,
    src.CITYC, src.ADRNR, src.IWERK, src.TXJCD, src.VTWEG, src.SPART, src.SPRAS,
    src.WKSOP, src.AWSLS, src.CHAZV_OLD, src.VLFKZ, src.BZIRK, src.ZONE1,
    src.TAXIW, src.BZQHL, src.LET01, src.LET02, src.LET03, src.TXNAM_MA1,
    src.TXNAM_MA2, src.TXNAM_MA3, src.BETOL, src.J_1BBRANCH, src.VTBFI,
    src.FPRFW, src.ACHVM, src.DVSART, src.NODETYPE, src.NSCHEMA, src.PKOSA,
    src.MISCH, src.MGVUPD, src.VSTEL, src.MGVLAUPD, src.MGVLAREVAL,
    src.SOURCING, src.NO_DEFAULT_BATCH_MANAGEMENT, src.FSH_MG_ARUN_REQ,
    src.FSH_SEAIM, src.FSH_BOM_MAINTENANCE, src.FSH_GROUP_PR, src.ARUN_FIX_BATCH,
    src.OILIVAL, src.OIHVTYPE, src.OIHCREDIPI, src.STORETYPE, src.DEP_STORE,
    src._LOADED_AT::DATE, src._LOADED_AT, src._BATCH_ID, src._SOURCE_SYSTEM,
    src._BUSINESS_DATE
);

-- ---------------------------------------------------------------------------
-- 2. Scheduled TASK at 21:00 UTC (30 min after the existing RFC sync window).
--    Created suspended; resume only after manual MERGE verified once.
--    DELETE-by-source intentionally omitted - protects against a 0-row load
--    wiping BRONZE.T001W. Add a row-count guard before flipping that on.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE TASK V2RETAIL.RAW_SAP_MASTER.SYNC_T001W_TO_BRONZE
    WAREHOUSE = V2_WH
    SCHEDULE  = 'USING CRON 0 21 * * * UTC'
    COMMENT   = 'Promotes raw T001W snapshot into BRONZE.T001W. Paired with rfc-api TableDump endpoint READ_T001W.'
AS
    MERGE INTO V2RETAIL.BRONZE.T001W AS tgt
    USING V2RETAIL.RAW_SAP_MASTER.T001W AS src
       ON tgt.MANDT = src.MANDT AND tgt.WERKS = src.WERKS
    WHEN MATCHED THEN UPDATE SET
        tgt.NAME1 = src.NAME1, tgt.BWKEY = src.BWKEY, tgt.KUNNR = src.KUNNR,
        tgt.LIFNR = src.LIFNR, tgt.FABKL = src.FABKL, tgt.NAME2 = src.NAME2,
        tgt.STRAS = src.STRAS, tgt.PFACH = src.PFACH, tgt.PSTLZ = src.PSTLZ,
        tgt.ORT01 = src.ORT01, tgt.EKORG = src.EKORG, tgt.VKORG = src.VKORG,
        tgt.CHAZV = src.CHAZV, tgt.KKOWK = src.KKOWK, tgt.KORDB = src.KORDB,
        tgt.BEDPL = src.BEDPL, tgt.LAND1 = src.LAND1, tgt.REGIO = src.REGIO,
        tgt.COUNC = src.COUNC, tgt.CITYC = src.CITYC, tgt.ADRNR = src.ADRNR,
        tgt.IWERK = src.IWERK, tgt.TXJCD = src.TXJCD, tgt.VTWEG = src.VTWEG,
        tgt.SPART = src.SPART, tgt.SPRAS = src.SPRAS, tgt.WKSOP = src.WKSOP,
        tgt.AWSLS = src.AWSLS, tgt.CHAZV_OLD = src.CHAZV_OLD, tgt.VLFKZ = src.VLFKZ,
        tgt.BZIRK = src.BZIRK, tgt.ZONE1 = src.ZONE1, tgt.TAXIW = src.TAXIW,
        tgt.BZQHL = src.BZQHL, tgt.LET01 = src.LET01, tgt.LET02 = src.LET02,
        tgt.LET03 = src.LET03, tgt.TXNAM_MA1 = src.TXNAM_MA1, tgt.TXNAM_MA2 = src.TXNAM_MA2,
        tgt.TXNAM_MA3 = src.TXNAM_MA3, tgt.BETOL = src.BETOL, tgt.J_1BBRANCH = src.J_1BBRANCH,
        tgt.VTBFI = src.VTBFI, tgt.FPRFW = src.FPRFW, tgt.ACHVM = src.ACHVM,
        tgt.DVSART = src.DVSART, tgt.NODETYPE = src.NODETYPE, tgt.NSCHEMA = src.NSCHEMA,
        tgt.PKOSA = src.PKOSA, tgt.MISCH = src.MISCH, tgt.MGVUPD = src.MGVUPD,
        tgt.VSTEL = src.VSTEL, tgt.MGVLAUPD = src.MGVLAUPD, tgt.MGVLAREVAL = src.MGVLAREVAL,
        tgt.SOURCING = src.SOURCING, tgt.NO_DEFAULT_BATCH_MANAGEMENT = src.NO_DEFAULT_BATCH_MANAGEMENT,
        tgt.FSH_MG_ARUN_REQ = src.FSH_MG_ARUN_REQ, tgt.FSH_SEAIM = src.FSH_SEAIM,
        tgt.FSH_BOM_MAINTENANCE = src.FSH_BOM_MAINTENANCE, tgt.FSH_GROUP_PR = src.FSH_GROUP_PR,
        tgt.ARUN_FIX_BATCH = src.ARUN_FIX_BATCH, tgt.OILIVAL = src.OILIVAL,
        tgt.OIHVTYPE = src.OIHVTYPE, tgt.OIHCREDIPI = src.OIHCREDIPI,
        tgt.STORETYPE = src.STORETYPE, tgt.DEP_STORE = src.DEP_STORE,
        tgt.EXTRACT_DATE = src._LOADED_AT::DATE,
        tgt._LOADED_AT = src._LOADED_AT, tgt._BATCH_ID = src._BATCH_ID,
        tgt._SOURCE_SYSTEM = src._SOURCE_SYSTEM, tgt._BUSINESS_DATE = src._BUSINESS_DATE
    WHEN NOT MATCHED THEN INSERT (
        MANDT, WERKS, NAME1, BWKEY, KUNNR, LIFNR, FABKL, NAME2, STRAS, PFACH, PSTLZ,
        ORT01, EKORG, VKORG, CHAZV, KKOWK, KORDB, BEDPL, LAND1, REGIO, COUNC, CITYC,
        ADRNR, IWERK, TXJCD, VTWEG, SPART, SPRAS, WKSOP, AWSLS, CHAZV_OLD, VLFKZ,
        BZIRK, ZONE1, TAXIW, BZQHL, LET01, LET02, LET03, TXNAM_MA1, TXNAM_MA2,
        TXNAM_MA3, BETOL, J_1BBRANCH, VTBFI, FPRFW, ACHVM, DVSART, NODETYPE,
        NSCHEMA, PKOSA, MISCH, MGVUPD, VSTEL, MGVLAUPD, MGVLAREVAL, SOURCING,
        NO_DEFAULT_BATCH_MANAGEMENT, FSH_MG_ARUN_REQ, FSH_SEAIM, FSH_BOM_MAINTENANCE,
        FSH_GROUP_PR, ARUN_FIX_BATCH, OILIVAL, OIHVTYPE, OIHCREDIPI, STORETYPE,
        DEP_STORE, EXTRACT_DATE, _LOADED_AT, _BATCH_ID, _SOURCE_SYSTEM, _BUSINESS_DATE
    ) VALUES (
        src.MANDT, src.WERKS, src.NAME1, src.BWKEY, src.KUNNR, src.LIFNR, src.FABKL,
        src.NAME2, src.STRAS, src.PFACH, src.PSTLZ, src.ORT01, src.EKORG, src.VKORG,
        src.CHAZV, src.KKOWK, src.KORDB, src.BEDPL, src.LAND1, src.REGIO, src.COUNC,
        src.CITYC, src.ADRNR, src.IWERK, src.TXJCD, src.VTWEG, src.SPART, src.SPRAS,
        src.WKSOP, src.AWSLS, src.CHAZV_OLD, src.VLFKZ, src.BZIRK, src.ZONE1,
        src.TAXIW, src.BZQHL, src.LET01, src.LET02, src.LET03, src.TXNAM_MA1,
        src.TXNAM_MA2, src.TXNAM_MA3, src.BETOL, src.J_1BBRANCH, src.VTBFI,
        src.FPRFW, src.ACHVM, src.DVSART, src.NODETYPE, src.NSCHEMA, src.PKOSA,
        src.MISCH, src.MGVUPD, src.VSTEL, src.MGVLAUPD, src.MGVLAREVAL,
        src.SOURCING, src.NO_DEFAULT_BATCH_MANAGEMENT, src.FSH_MG_ARUN_REQ,
        src.FSH_SEAIM, src.FSH_BOM_MAINTENANCE, src.FSH_GROUP_PR, src.ARUN_FIX_BATCH,
        src.OILIVAL, src.OIHVTYPE, src.OIHCREDIPI, src.STORETYPE, src.DEP_STORE,
        src._LOADED_AT::DATE, src._LOADED_AT, src._BATCH_ID, src._SOURCE_SYSTEM,
        src._BUSINESS_DATE
    );

-- ALTER TASK V2RETAIL.RAW_SAP_MASTER.SYNC_T001W_TO_BRONZE RESUME;

-- ---------------------------------------------------------------------------
-- 3. Health-check view
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW V2RETAIL.RAW_SAP_MASTER.VW_T001W_HEALTH AS
SELECT
    (SELECT COUNT(*) FROM V2RETAIL.RAW_SAP_MASTER.T001W)               AS raw_rows,
    (SELECT COUNT(*) FROM V2RETAIL.BRONZE.T001W)                       AS bronze_rows,
    (SELECT MAX(_LOADED_AT) FROM V2RETAIL.RAW_SAP_MASTER.T001W)        AS raw_last_loaded,
    (SELECT MAX(_LOADED_AT) FROM V2RETAIL.BRONZE.T001W)                AS bronze_last_loaded,
    DATEDIFF('minute',
        (SELECT MAX(_LOADED_AT) FROM V2RETAIL.RAW_SAP_MASTER.T001W),
        (SELECT MAX(_LOADED_AT) FROM V2RETAIL.BRONZE.T001W)
    ) AS merge_lag_minutes;
