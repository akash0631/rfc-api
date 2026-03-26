/**
 * V2 Retail DataV2 — Full Schema Crawler
 * Run: node schema_crawler.js
 * Output: schema_audit.json (in same folder)
 * 
 * For each table it captures:
 *  - Row count
 *  - All columns (name, type, nullable, max_length)
 *  - Date range (min/max) for any date/datetime columns
 *  - 3 sample rows
 *  - Inferred category (SALES / STOCK / FINANCE / MASTER / LOG / ARCHIVE / OTHER)
 *  - Staleness flag (no data in last 90 days)
 *  - Duplicate candidate detection (same columns as another table)
 */

const sql = require('mssql');
const fs  = require('fs');

const CONFIG = {
  server:   '192.168.151.28',
  database: 'DataV2',
  user:     'sa',
  password: 'vrl@55555',
  port:     1433,
  options:  { trustServerCertificate: true, connectTimeout: 30000, requestTimeout: 120000 }
};

// ── Category keyword maps ──────────────────────────────────────
const CATEGORY_RULES = [
  { cat: 'SALES',          kw: ['SALE','TXN','TRANSACTION','REVENUE','BILLING','INVOICE','ORDER','POS','RETAIL'] },
  { cat: 'STOCK',          kw: ['STOCK','INVENTORY','WH','WAREHOUSE','PUTAWAY','PICKING','STOREAREA','STK','REPLEN'] },
  { cat: 'STOCK_TRANSFER', kw: ['TRANSFER','STO','INTERSTORE','MOVEMENT','MVT','GOODS_MVT','GIT'] },
  { cat: 'INWARD_GRC',     kw: ['GRC','INWARD','GRN','RECEIPT','DELIVERY','PUTWAY','HU_','VEHICLE'] },
  { cat: 'PURCHASE',       kw: ['PO','PURCHASE','VENDOR','SUPPLIER','PROCUREMENT','PR_','PO_'] },
  { cat: 'FINANCE',        kw: ['FIN','LEDGER','GL','PAYMENT','ADVANCE','DEDUCTION','CREDIT','DEBIT','ACCOUNT','COST','MARGIN'] },
  { cat: 'ARTICLE_MASTER', kw: ['ARTICLE','ART','MATNR','MATERIAL','STYLE','COLOR','SIZE','PRODUCT','CATEGORY','GEN_'] },
  { cat: 'STORE_MASTER',   kw: ['STORE','PLANT','WERKS','BRANCH','LOCATION','ZONE','REGION'] },
  { cat: 'PLANNING',       kw: ['PLAN','FORECAST','BUDGET','TARGET','ALLOC','ASSORT','OTB','TREND','KPI'] },
  { cat: 'LOG_AUDIT',      kw: ['LOG','AUDIT','TRACK','HISTORY','CHANGE','ERROR','DEBUG','PROCESS'] },
  { cat: 'FESTIVAL',       kw: ['FESTIVAL','EVENT','SEASON','OCCASION'] },
  { cat: 'SAMPLING',       kw: ['SAMPLE','QC','QUALITY','INSPECTION'] },
];

function inferCategory(tableName) {
  const t = tableName.toUpperCase();
  for (const rule of CATEGORY_RULES) {
    if (rule.kw.some(k => t.includes(k))) return rule.cat;
  }
  return 'OTHER';
}

function inferRelevance(tableName, rowCount, latestDate) {
  const t = tableName.toUpperCase();
  // Archive candidates: old prefixes, test tables, temp tables
  if (/^(OLD_|BAK_|BACKUP_|TEMP_|TMP_|TEST_|_OLD|_BAK|_BCK|_COPY)/.test(t)) return 'ARCHIVE_CANDIDATE';
  if (rowCount === 0) return 'EMPTY';
  if (latestDate) {
    const daysSince = (Date.now() - new Date(latestDate).getTime()) / 86400000;
    if (daysSince > 365) return 'STALE_1Y';
    if (daysSince > 90)  return 'STALE_90D';
  }
  return 'ACTIVE';
}

// ── Main crawler ───────────────────────────────────────────────
async function crawl() {
  const startTime = Date.now();
  console.log('[CRAWLER] Connecting to DataV2 on 192.168.151.28...');
  
  const pool = await sql.connect(CONFIG);
  console.log('[CRAWLER] Connected. Starting full schema audit...\n');

  // 1. Get all tables with row counts from sys tables (fast)
  const tablesResult = await pool.request().query(`
    SELECT 
      t.TABLE_NAME,
      p.rows AS ROW_COUNT
    FROM INFORMATION_SCHEMA.TABLES t
    LEFT JOIN sys.tables st ON st.name = t.TABLE_NAME
    LEFT JOIN sys.partitions p ON p.object_id = st.object_id AND p.index_id IN (0,1)
    WHERE t.TABLE_TYPE = 'BASE TABLE'
    ORDER BY p.rows DESC
  `);
  
  const tables = tablesResult.recordset;
  console.log(`[CRAWLER] Found ${tables.length} tables. Beginning column + sample crawl...\n`);

  // 2. Get ALL column metadata in one shot (much faster than per-table queries)
  const columnsResult = await pool.request().query(`
    SELECT 
      TABLE_NAME,
      COLUMN_NAME,
      DATA_TYPE,
      IS_NULLABLE,
      CHARACTER_MAXIMUM_LENGTH,
      NUMERIC_PRECISION,
      NUMERIC_SCALE,
      ORDINAL_POSITION
    FROM INFORMATION_SCHEMA.COLUMNS
    ORDER BY TABLE_NAME, ORDINAL_POSITION
  `);

  // Group columns by table
  const columnsByTable = {};
  for (const col of columnsResult.recordset) {
    if (!columnsByTable[col.TABLE_NAME]) columnsByTable[col.TABLE_NAME] = [];
    columnsByTable[col.TABLE_NAME].push({
      name:       col.COLUMN_NAME,
      type:       col.DATA_TYPE,
      nullable:   col.IS_NULLABLE === 'YES',
      max_length: col.CHARACTER_MAXIMUM_LENGTH,
      precision:  col.NUMERIC_PRECISION,
      scale:      col.NUMERIC_SCALE
    });
  }

  const audit = [];
  const fingerprints = {}; // for duplicate detection

  // 3. Per-table: date range + sample rows
  for (let i = 0; i < tables.length; i++) {
    const { TABLE_NAME, ROW_COUNT } = tables[i];
    const rowCount = parseInt(ROW_COUNT) || 0;
    const cols = columnsByTable[TABLE_NAME] || [];
    
    if ((i + 1) % 50 === 0 || i < 5) {
      const elapsed = ((Date.now() - startTime) / 1000).toFixed(0);
      console.log(`[CRAWLER] Progress: ${i+1}/${tables.length} tables | ${elapsed}s elapsed`);
    }

    // Identify date/datetime columns
    const dateCols = cols.filter(c => ['date','datetime','datetime2','smalldatetime'].includes(c.type));
    // Identify amount/value columns  
    const amountCols = cols.filter(c => ['decimal','numeric','float','money','smallmoney'].includes(c.type) && 
      /AMOUNT|VALUE|PRICE|COST|RATE|QTY|QUANTITY|SALES|MARGIN|GM/.test(c.name.toUpperCase()));

    let dateRange = null;
    let sampleRows = [];

    if (rowCount > 0) {
      // Get date range from first date column found
      if (dateCols.length > 0) {
        try {
          const dc = dateCols[0].name;
          const dr = await pool.request().query(`
            SELECT MIN([${dc}]) as min_date, MAX([${dc}]) as max_date 
            FROM [${TABLE_NAME}] WITH(NOLOCK)
          `);
          const row = dr.recordset[0];
          if (row.min_date) {
            dateRange = {
              column:   dc,
              min_date: row.min_date ? new Date(row.min_date).toISOString().split('T')[0] : null,
              max_date: row.max_date ? new Date(row.max_date).toISOString().split('T')[0] : null
            };
          }
        } catch(e) { /* skip if date query fails */ }
      }

      // Get 5 sample rows (skip if table is >500M rows to avoid timeout)
      const tooLarge = rowCount > 500000000;
      try {
        if (!tooLarge) {
        const sr = await pool.request().query(`SELECT TOP 5 * FROM [${TABLE_NAME}] WITH(NOLOCK)`);
        sampleRows = sr.recordset.map(row => {
          const clean = {};
          for (const [k, v] of Object.entries(row)) {
            clean[k] = v instanceof Date ? v.toISOString().split('T')[0] : v;
          }
          return clean;
        });
        } else {
          sampleRows = [{ _note: `Table has ${rowCount.toLocaleString()} rows — sample skipped to avoid timeout. Columns above describe the schema.` }];
        }
      } catch(e) { sampleRows = [{ _error: e.message }]; }
    }

    // Build column fingerprint for duplicate detection
    const fingerprint = cols.map(c => `${c.name}:${c.type}`).sort().join('|');
    if (!fingerprints[fingerprint]) fingerprints[fingerprint] = [];
    fingerprints[fingerprint].push(TABLE_NAME);

    // Determine latest date for staleness check
    const latestDate = dateRange?.max_date || null;
    const category   = inferCategory(TABLE_NAME);
    const relevance  = inferRelevance(TABLE_NAME, rowCount, latestDate);

    audit.push({
      table_name:    TABLE_NAME,
      row_count:     rowCount,
      column_count:  cols.length,
      category,
      relevance,
      date_range:    dateRange,
      date_columns:  dateCols.map(c => c.name),
      amount_columns: amountCols.map(c => c.name),
      columns:       cols,
      sample_rows:   sampleRows,
      duplicate_of:  null  // filled in next step
    });
  }

  // 4. Mark duplicates
  for (const entry of audit) {
    const cols = entry.columns.map(c => `${c.name}:${c.type}`).sort().join('|');
    const dupes = fingerprints[cols].filter(t => t !== entry.table_name);
    if (dupes.length > 0) entry.duplicate_of = dupes;
  }

  // 5. Build summary stats
  const summary = {
    generated_at:    new Date().toISOString(),
    database:        'DataV2',
    server:          '192.168.151.28',
    total_tables:    audit.length,
    total_rows_est:  audit.reduce((s, t) => s + t.row_count, 0),
    by_category:     {},
    by_relevance:    {},
    duplicate_groups: Object.values(fingerprints).filter(g => g.length > 1),
    empty_tables:    audit.filter(t => t.row_count === 0).map(t => t.table_name),
    stale_tables:    audit.filter(t => t.relevance.startsWith('STALE')).map(t => ({ table: t.table_name, relevance: t.relevance, last_date: t.date_range?.max_date })),
    archive_candidates: audit.filter(t => t.relevance === 'ARCHIVE_CANDIDATE').map(t => t.table_name),
    top_tables_by_rows: audit.slice(0, 20).map(t => ({ table: t.table_name, rows: t.row_count, category: t.category }))
  };

  for (const t of audit) {
    summary.by_category[t.category] = (summary.by_category[t.category] || 0) + 1;
    summary.by_relevance[t.relevance] = (summary.by_relevance[t.relevance] || 0) + 1;
  }

  const output = { summary, tables: audit };

  // 6. Write output
  const outPath = 'C:\\V2SqlProxy\\schema_audit.json';
  fs.writeFileSync(outPath, JSON.stringify(output, null, 2));
  
  const elapsed = ((Date.now() - startTime) / 1000 / 60).toFixed(1);
  console.log(`\n[CRAWLER] ✓ COMPLETE in ${elapsed} minutes`);
  console.log(`[CRAWLER] Output: ${outPath}`);
  console.log(`[CRAWLER] Tables audited: ${audit.length}`);
  console.log(`[CRAWLER] Total rows (est): ${summary.total_rows_est.toLocaleString()}`);
  console.log(`\n[CRAWLER] Category breakdown:`);
  for (const [cat, count] of Object.entries(summary.by_category).sort((a,b)=>b[1]-a[1])) {
    console.log(`  ${cat.padEnd(20)} ${count} tables`);
  }
  console.log(`\n[CRAWLER] Relevance breakdown:`);
  for (const [rel, count] of Object.entries(summary.by_relevance).sort((a,b)=>b[1]-a[1])) {
    console.log(`  ${rel.padEnd(20)} ${count} tables`);
  }
  console.log(`\n[CRAWLER] Duplicate groups found: ${summary.duplicate_groups.length}`);
  console.log(`[CRAWLER] Empty tables: ${summary.empty_tables.length}`);
  
  await pool.close();
  console.log('\n[CRAWLER] Done. Upload schema_audit.json to Claude to generate the full document.');
}

crawl().catch(err => {
  console.error('[CRAWLER] FATAL:', err.message);
  process.exit(1);
});
