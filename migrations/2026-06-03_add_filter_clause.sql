-- Migration: add FILTER_CLAUSE + REQUIRES_FILTER columns to GOLD.RFC_MASTER
-- Applied 2026-06-03 by Shubham
-- Companion to PR: feat/filter-clause-support
--
-- Why: SAP safety. When dumping huge raw SAP tables (MARD, MARC, MSEG, EKPO, ...)
-- via RFC_READ_TABLE, an empty WHERE clause causes a full-table scan that can
-- peg SAP work processes. FILTER_CLAUSE flows to RFC_READ_TABLE.OPTIONS.
-- REQUIRES_FILTER=TRUE rejects exec at the wrapper level when FILTER_CLAUSE is empty.
--
-- Used by EXECUTION_PATTERN='TableDump' only. Ignored by other patterns.

ALTER TABLE V2RETAIL.GOLD.RFC_MASTER
  ADD COLUMN IF NOT EXISTS FILTER_CLAUSE VARCHAR(2000)
  COMMENT 'WHERE-clause text passed to RFC_READ_TABLE.OPTIONS. e.g. "WERKS = ''HB05'' AND LOEKZ <> ''L''". Required for huge tables (MARD/MARC/MSEG/EKPO/...).';

ALTER TABLE V2RETAIL.GOLD.RFC_MASTER
  ADD COLUMN IF NOT EXISTS REQUIRES_FILTER BOOLEAN DEFAULT FALSE
  COMMENT 'If TRUE, server rejects exec when FILTER_CLAUSE is empty. SAP safety guard for huge tables.';

-- Verify
-- SELECT RFC_CODE, EXECUTION_PATTERN, SOURCE_TABLE, FILTER_CLAUSE, REQUIRES_FILTER
-- FROM V2RETAIL.GOLD.RFC_MASTER
-- WHERE EXECUTION_PATTERN = 'TableDump';
