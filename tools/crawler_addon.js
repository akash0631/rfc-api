
// ── BUILT-IN SCHEMA CRAWLER ──────────────────────────────────
let crawlerStatus = { running: false, tables_done: 0, tables_total: 0, started_at: null };

const CATEGORY_RULES = [
  { cat: 'SALES',          kw: ['SALE','TXN','TRANSACTION','REVENUE','BILLING','POS'] },
  { cat: 'STOCK',          kw: ['STOCK','INVENTORY','STOREAREA','STK','REPLEN','WM_STK','MSA_STK'] },
  { cat: 'STOCK_TRANSFER', kw: ['TRANSFER','STO','INTERSTORE','MVT','GOODS_MVT','GIT'] },
  { cat: 'INWARD_GRC',     kw: ['GRC','INWARD','GRN','RECEIPT','PUTWAY','HU_','VEHICLE'] },
  { cat: 'PURCHASE',       kw: ['PO_','PURCHASE','VENDOR','PROCUREMENT'] },
  { cat: 'FINANCE',        kw: ['FIN','LEDGER','GL','PAYMENT','ADVANCE','DEDUCTION','CREDIT','DEBIT','COST','MARGIN'] },
  { cat: 'ARTICLE_MASTER', kw: ['ARTICLE','ART_GEN','MATNR','MATERIAL','STYLE','GEN_ART'] },
  { cat: 'STORE_MASTER',   kw: ['STORE','PLANT','WERKS','BRANCH','ZONE','REGION','STORE_MASTER'] },
  { cat: 'PLANNING',       kw: ['PLAN','FORECAST','BUDGET','TARGET','ALLOC','TREND','KPI','SEASONAL','FESTIVAL_PLN'] },
  { cat: 'LOG_AUDIT',      kw: ['LOG','AUDIT','TRACK','HISTORY','CHANGE','ERROR','PROCESS'] },
  { cat: 'FESTIVAL',       kw: ['FESTIVAL','EVENT','SEASON'] },
];

function inferCategory(t) {
  t = t.toUpperCase();
  for (const r of CATEGORY_RULES) if (r.kw.some(k => t.includes(k))) return r.cat;
  return 'OTHER';
}

function inferRelevance(t, rows, lastDate) {
  t = t.toUpperCase();
  if (/^(OLD_|BAK_|BACKUP_|TEMP_|TMP_|TEST_)/.test(t) || /(\_OLD|\_BAK|\_BCK|\_COPY)$/.test(t)) return 'ARCHIVE_CANDIDATE';
  if (rows === 0) return 'EMPTY';
  if (lastDate) {
    const days = (Date.now() - new Date(lastDate).getTime()) / 86400000;
    if (days > 365) return 'STALE_1Y';
    if (days > 90)  return 'STALE_90D';
  }
  return 'ACTIVE';
}

async function runCrawler() {
  if (crawlerStatus.running) return { error: 'Already running' };
  crawlerStatus = { running: true, tables_done: 0, tables_total: 0, started_at: new Date().toISOString() };
  console.log('[CRAWLER] Starting built-in crawler...');

  const fs = require('fs');
  const startTime = Date.now();

  try {
    // 1. Get tables with row counts
    const tRes = await pool.request().query(`
      SELECT t.TABLE_NAME, ISNULL(p.rows,0) AS ROW_COUNT
      FROM INFORMATION_SCHEMA.TABLES t
      LEFT JOIN sys.tables st ON st.name = t.TABLE_NAME
      LEFT JOIN sys.partitions p ON p.object_id = st.object_id AND p.index_id IN (0,1)
      WHERE t.TABLE_TYPE = 'BASE TABLE'
      ORDER BY p.rows DESC`);
    const tables = tRes.recordset;
    crawlerStatus.tables_total = tables.length;
    console.log('[CRAWLER] Found', tables.length, 'tables');

    // 2. Get all columns in one shot
    const cRes = await pool.request().query(`
      SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, ORDINAL_POSITION
      FROM INFORMATION_SCHEMA.COLUMNS ORDER BY TABLE_NAME, ORDINAL_POSITION`);
    const colsByTable = {};
    for (const c of cRes.recordset) {
      if (!colsByTable[c.TABLE_NAME]) colsByTable[c.TABLE_NAME] = [];
      colsByTable[c.TABLE_NAME].push({ name: c.COLUMN_NAME, type: c.DATA_TYPE, nullable: c.IS_NULLABLE === 'YES', max_length: c.CHARACTER_MAXIMUM_LENGTH });
    }

    const audit = [];
    const fingerprints = {};

    // 3. Per-table crawl
    for (let i = 0; i < tables.length; i++) {
      const { TABLE_NAME, ROW_COUNT } = tables[i];
      const rowCount = parseInt(ROW_COUNT) || 0;
      const cols = colsByTable[TABLE_NAME] || [];
      const dateCols = cols.filter(c => ['date','datetime','datetime2','smalldatetime'].includes(c.type));
      const skipHeavy = rowCount > 5000000;

      let dateRange = null;
      let sampleRows = [];

      if (rowCount > 0) {
        // Date range - only for small tables
        if (dateCols.length > 0 && !skipHeavy) {
          try {
            const dc = dateCols[0].name;
            const sql = 'SELECT MIN([' + dc + ']) as mn, MAX([' + dc + ']) as mx FROM [' + TABLE_NAME + '] WITH(NOLOCK)';
            const dr = await pool.request().query(sql);
            const row = dr.recordset[0];
            if (row.mn) dateRange = { column: dc, min_date: new Date(row.mn).toISOString().split('T')[0], max_date: new Date(row.mx).toISOString().split('T')[0] };
          } catch(e) {}
        } else if (dateCols.length > 0) {
          dateRange = { column: dateCols[0].name, min_date: 'large_table', max_date: 'large_table' };
        }

        // Samples - only for tables under 10M rows
        if (rowCount <= 10000000) {
          try {
            const sr = await pool.request().query('SELECT TOP 2 * FROM [' + TABLE_NAME + '] WITH(NOLOCK)');
            sampleRows = sr.recordset.map(row => {
              const clean = {};
              for (const [k,v] of Object.entries(row)) clean[k] = v instanceof Date ? v.toISOString().split('T')[0] : v;
              return clean;
            });
          } catch(e) {}
        } else {
          sampleRows = [{ _note: 'Large table (' + rowCount.toLocaleString() + ' rows) - sample skipped' }];
        }
      }

      const fp = cols.map(c => c.name + ':' + c.type).sort().join('|');
      if (!fingerprints[fp]) fingerprints[fp] = [];
      fingerprints[fp].push(TABLE_NAME);

      const latestDate = dateRange && dateRange.max_date !== 'large_table' ? dateRange.max_date : null;
      audit.push({
        table_name: TABLE_NAME, row_count: rowCount, column_count: cols.length,
        category: inferCategory(TABLE_NAME), relevance: inferRelevance(TABLE_NAME, rowCount, latestDate),
        date_range: dateRange, date_columns: dateCols.map(c => c.name),
        columns: cols, sample_rows: sampleRows, duplicate_of: null
      });

      crawlerStatus.tables_done = i + 1;

      // Checkpoint every 50 tables
      if ((i + 1) % 50 === 0) {
        for (const entry of audit) {
          const fp2 = entry.columns.map(c => c.name + ':' + c.type).sort().join('|');
          const dupes = (fingerprints[fp2] || []).filter(t => t !== entry.table_name);
          if (dupes.length > 0) entry.duplicate_of = dupes;
        }
        const checkpoint = { checkpoint: true, tables_done: i+1, tables_total: tables.length, generated_at: new Date().toISOString(), elapsed_mins: ((Date.now()-startTime)/60000).toFixed(1), tables: audit };
        fs.writeFileSync('C:\\V2SqlProxy\\schema_checkpoint.json', JSON.stringify(checkpoint, null, 2));
        console.log('[CRAWLER] >>> Checkpoint saved:', i+1, 'tables <<<');
      }
      if ((i + 1) % 100 === 0) console.log('[CRAWLER] Progress:', i+1 + '/' + tables.length, '|', ((Date.now()-startTime)/1000).toFixed(0) + 's elapsed');
    }

    // Mark duplicates on final output
    for (const entry of audit) {
      const fp2 = entry.columns.map(c => c.name + ':' + c.type).sort().join('|');
      entry.duplicate_of = (fingerprints[fp2] || []).filter(t => t !== entry.table_name);
    }

    const summary = {
      generated_at: new Date().toISOString(), database: 'DataV2', total_tables: audit.length,
      total_rows_est: audit.reduce((s,t) => s + t.row_count, 0),
      by_category: {}, by_relevance: {},
      duplicate_groups: Object.values(fingerprints).filter(g => g.length > 1),
      empty_tables: audit.filter(t => t.row_count === 0).map(t => t.table_name),
      stale_tables: audit.filter(t => t.relevance.startsWith('STALE')).map(t => ({ table: t.table_name, relevance: t.relevance, last_date: t.date_range && t.date_range.max_date })),
      top_tables_by_rows: audit.slice(0,20).map(t => ({ table: t.table_name, rows: t.row_count, category: t.category }))
    };
    for (const t of audit) {
      summary.by_category[t.category] = (summary.by_category[t.category] || 0) + 1;
      summary.by_relevance[t.relevance] = (summary.by_relevance[t.relevance] || 0) + 1;
    }

    const output = { summary, tables: audit };
    fs.writeFileSync('C:\\V2SqlProxy\\schema_audit.json', JSON.stringify(output, null, 2));
    const mins = ((Date.now()-startTime)/60000).toFixed(1);
    console.log('[CRAWLER] COMPLETE in', mins, 'minutes. schema_audit.json saved.');
    crawlerStatus.running = false;
    crawlerStatus.completed_at = new Date().toISOString();
    crawlerStatus.elapsed_mins = mins;
  } catch(err) {
    console.error('[CRAWLER] FATAL:', err.message);
    crawlerStatus.running = false;
    crawlerStatus.error = err.message;
  }
}

app.post('/start-crawler', (req, res) => {
  if (crawlerStatus.running) return res.json({ status: 'already_running', ...crawlerStatus });
  runCrawler(); // async - don't await
  res.json({ status: 'started', message: 'Crawler started. Poll /checkpoint every 5 mins.' });
});

app.get('/crawler-status', (req, res) => res.json(crawlerStatus));

