using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Services
{
    /// <summary>
    /// Wraps SAP's standard RFC_READ_TABLE for bulk SAP-table dumps.
    /// Handles the 512-byte WA buffer constraint by:
    ///   1. Pulling FIELDS metadata via NO_DATA='X' (no buffer limit on metadata-only call)
    ///   2. Resolving the primary key from DD03L (KEYFLAG='X' ordered by POSITION)
    ///   3. Splitting fields into chunks &lt;= MaxRowBytes, with PK repeated in every chunk
    ///   4. Reading each chunk and stitching rows back together by PK
    ///
    /// Used by the TableDump branch of /api/execute/{rfcCode}/sync to land raw SAP
    /// tables into Snowflake (typically RAW_SAP_*) atomically via the matching
    /// SnowflakeService.BulkLoadViaStage call.
    ///
    /// Reuses BaseController.rfcConfigparametersproduction() for the PROD SAP
    /// connection. UAT support deferred to v0.2 (caller passes env).
    /// </summary>
    public class SapTableDumpService
    {
        public const int DEFAULT_MAX_ROW_BYTES = 480;   // SAP WA buffer is 512; 480 leaves a safety margin.

        public struct FieldMeta
        {
            public string Name;
            public int Offset;
            public int Length;
        }

        /// <summary>
        /// Read the full SAP table, chunked + stitched. Returns column metadata in SAP
        /// declaration order plus stitched rows as dict[FIELDNAME]=value (all strings).
        /// </summary>
        public (List<FieldMeta> Columns, List<Dictionary<string, object>> Rows) ReadFullTable(
            string sapTable,
            List<string> requestedFields,
            string delimiter = "|",
            int maxRowBytes = DEFAULT_MAX_ROW_BYTES,
            int timeoutSeconds = 120)
        {
            var rfcPar = BaseController.rfcConfigparametersproduction();
            var dest = RfcDestinationManager.GetDestination(rfcPar);
            var repo = dest.Repository;

            // 1. Metadata via NO_DATA - bypasses the 512-byte cap because no rows come back.
            var allFields = GetFieldMetadata(repo, dest, sapTable, requestedFields);
            if (allFields.Count == 0)
                throw new InvalidOperationException("RFC_READ_TABLE returned no field metadata for " + sapTable);

            // 2. Primary key from DD03L (used to stitch chunked reads back together).
            var pkAll = GetPrimaryKey(repo, dest, sapTable);
            var allNames = new HashSet<string>(allFields.Select(f => f.Name));
            var pkInScope = pkAll.Where(p => allNames.Contains(p)).ToList();

            // 3. Split fields into chunks that fit the WA buffer.
            var chunks = ChunkFields(allFields, pkInScope, maxRowBytes);

            // 4. Read each chunk, stitch by PK (or by row index if no PK).
            bool stitchByPk = pkInScope.Count > 0;
            var byKey = new Dictionary<string, Dictionary<string, object>>();
            var byIdx = new List<Dictionary<string, object>>();

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var rows = ReadChunk(repo, dest, sapTable, chunks[ci], delimiter, 0, 0, null);

                if (stitchByPk)
                {
                    foreach (var r in rows)
                    {
                        string key = string.Join("\x01", pkInScope.Select(p =>
                            r.ContainsKey(p) ? r[p]?.ToString() : ""));
                        Dictionary<string, object> merged;
                        if (!byKey.TryGetValue(key, out merged))
                        {
                            merged = new Dictionary<string, object>();
                            byKey[key] = merged;
                        }
                        foreach (var kv in r) merged[kv.Key] = kv.Value;
                    }
                }
                else
                {
                    if (ci == 0)
                    {
                        byIdx = rows;
                    }
                    else
                    {
                        if (rows.Count != byIdx.Count)
                            throw new InvalidOperationException(
                                "Chunk row count mismatch (" + rows.Count + " vs " + byIdx.Count +
                                ") and no PK to stitch - aborting.");
                        for (int i = 0; i < rows.Count; i++)
                            foreach (var kv in rows[i]) byIdx[i][kv.Key] = kv.Value;
                    }
                }
            }

            var stitched = stitchByPk ? byKey.Values.ToList() : byIdx;
            return (allFields, stitched);
        }

        // ── Lower-level helpers (public for testability) ────────────────────────────

        /// <summary>Metadata only: NO_DATA='X' returns FIELDS info without populating DATA.</summary>
        public List<FieldMeta> GetFieldMetadata(
            RfcRepository repo, RfcDestination dest, string tableName, List<string> requestedFields)
        {
            IRfcFunction f = repo.CreateFunction("RFC_READ_TABLE");
            f.SetValue("QUERY_TABLE", tableName);
            f.SetValue("NO_DATA", "X");

            IRfcTable fields = f.GetTable("FIELDS");
            foreach (var name in requestedFields ?? new List<string>())
            {
                fields.Append();
                fields.SetValue("FIELDNAME", name);
            }

            f.Invoke(dest);

            IRfcTable ret = f.GetTable("FIELDS");
            var meta = new List<FieldMeta>();
            for (int i = 0; i < ret.RowCount; i++)
            {
                ret.CurrentIndex = i;
                meta.Add(new FieldMeta
                {
                    Name = ret.GetString("FIELDNAME").Trim(),
                    Offset = ret.GetInt("OFFSET"),
                    Length = ret.GetInt("LENGTH")
                });
            }
            return meta;
        }

        /// <summary>Look up KEYFLAG='X' fields in DD03L, ordered by POSITION.</summary>
        public List<string> GetPrimaryKey(RfcRepository repo, RfcDestination dest, string tableName)
        {
            try
            {
                var rows = ReadChunk(
                    repo, dest, "DD03L",
                    new List<string> { "FIELDNAME", "POSITION", "KEYFLAG" },
                    "|", 0, 0,
                    new[] { "TABNAME = '" + tableName.ToUpper() + "' AND KEYFLAG = 'X'" });

                var pks = new List<KeyValuePair<string, int>>();
                foreach (var row in rows)
                {
                    string fname = row.ContainsKey("FIELDNAME") ? row["FIELDNAME"]?.ToString() : null;
                    if (string.IsNullOrEmpty(fname)) continue;
                    int pos = 0;
                    int.TryParse(row.ContainsKey("POSITION") ? row["POSITION"]?.ToString() : "0", out pos);
                    pks.Add(new KeyValuePair<string, int>(fname, pos));
                }
                return pks.OrderBy(x => x.Value).Select(x => x.Key).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SapTableDumpService] DD03L lookup failed for " + tableName + ": " + ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Split fields so each chunk's row width fits maxBytes. PK fields are repeated
        /// in every chunk for stitching. Throws if a single field or the PK alone exceeds budget.
        /// </summary>
        public static List<List<string>> ChunkFields(
            List<FieldMeta> fields, List<string> pkFields, int maxBytes)
        {
            var pkSet = new HashSet<string>(pkFields);
            var pkMeta = fields.Where(f => pkSet.Contains(f.Name)).ToList();
            int pkBytes = pkMeta.Sum(f => f.Length) + pkMeta.Count;     // +1 per field for delimiter
            int budget = maxBytes - pkBytes;
            if (budget <= 0)
                throw new InvalidOperationException(
                    "Primary key fields alone (" + pkBytes + " bytes) exceed row width budget (" + maxBytes + ")");

            var nonPk = fields.Where(f => !pkSet.Contains(f.Name)).ToList();
            var chunks = new List<List<string>>();
            var current = new List<string>(pkFields);
            int currentBytes = 0;

            foreach (var f in nonPk)
            {
                int cost = f.Length + 1;
                if (cost > budget)
                    throw new InvalidOperationException(
                        "Single field " + f.Name + " (" + f.Length + " bytes) exceeds chunk budget (" + budget + ")");
                if (currentBytes + cost > budget && current.Count > pkFields.Count)
                {
                    chunks.Add(current);
                    current = new List<string>(pkFields);
                    currentBytes = 0;
                }
                current.Add(f.Name);
                currentBytes += cost;
            }
            if (current.Count > 0) chunks.Add(current);
            return chunks;
        }

        /// <summary>Read one chunk via RFC_READ_TABLE. Returns rows as dict[FIELDNAME]=value (string).</summary>
        public List<Dictionary<string, object>> ReadChunk(
            RfcRepository repo, RfcDestination dest, string tableName,
            List<string> chunkFields, string delimiter, int rowCount, int rowSkips,
            IEnumerable<string> options)
        {
            IRfcFunction f = repo.CreateFunction("RFC_READ_TABLE");
            f.SetValue("QUERY_TABLE", tableName);
            f.SetValue("DELIMITER", delimiter);
            if (rowCount > 0) f.SetValue("ROWCOUNT", rowCount);
            if (rowSkips > 0) f.SetValue("ROWSKIPS", rowSkips);

            IRfcTable fields = f.GetTable("FIELDS");
            foreach (var name in chunkFields)
            {
                fields.Append();
                fields.SetValue("FIELDNAME", name);
            }

            if (options != null)
            {
                IRfcTable opts = f.GetTable("OPTIONS");
                foreach (var clause in options)
                {
                    opts.Append();
                    opts.SetValue("TEXT", clause);
                }
            }

            f.Invoke(dest);

            IRfcTable ret = f.GetTable("FIELDS");
            var colMeta = new List<FieldMeta>();
            for (int i = 0; i < ret.RowCount; i++)
            {
                ret.CurrentIndex = i;
                colMeta.Add(new FieldMeta
                {
                    Name = ret.GetString("FIELDNAME").Trim(),
                    Offset = ret.GetInt("OFFSET"),
                    Length = ret.GetInt("LENGTH")
                });
            }

            IRfcTable data = f.GetTable("DATA");
            var rows = new List<Dictionary<string, object>>(data.RowCount);
            for (int i = 0; i < data.RowCount; i++)
            {
                data.CurrentIndex = i;
                string wa = data.GetString("WA");
                var row = new Dictionary<string, object>(colMeta.Count);
                foreach (var col in colMeta)
                {
                    int off = col.Offset;
                    int len = col.Length;
                    if (off + len > wa.Length) len = Math.Max(0, wa.Length - off);
                    row[col.Name] = off < wa.Length ? wa.Substring(off, len).Trim() : "";
                }
                rows.Add(row);
            }
            return rows;
        }
    }
}
