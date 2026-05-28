# BRONZE Landing for /api/* endpoints

Pulls from `sap-api.v2retail.net` /api/* and lands rows in `V2RETAIL.BRONZE.RFC_API_*`.

Runs in parallel to (and isolated from) Shubham's `rfc-bronze-sync.yml` pipeline.

| What | Detail |
|---|---|
| Source endpoints | `/api/po`, `/api/grn`, `/api/sales`, `/api/articles`, `/api/vendors`, `/api/plants`, `/api/article-colors` |
| Target | `V2RETAIL.BRONZE.RFC_API_*` tables |
| Schedule | 08:00 IST daily (`02:30 UTC`) — currently **disabled** pending GH secrets |
| Trigger | GHA workflow `api-to-bronze-sync.yml` |
| Audit | `BRONZE.RFC_API_SYNC_LOG` row per endpoint per run |

## Bring-up checklist

1. **Apply DDL** in SnowSight as ACCOUNTADMIN:
   ```
   migrations/2026-05-28_rfc_api_bronze_tables.sql
   ```
2. **Add GH secrets** to `akash0631/rfc-api`:
   - `RFC_API_KEY` = `v2-rfc-proxy-2026`
   - `SF_ACCOUNT` = `iafphkw-hh80816`
   - `SF_USER` = (service user, e.g. `SHUBHAM_SVC` or `POWERBI`)
   - `SF_ROLE` = `ACCOUNTADMIN` (or scoped role)
   - `SF_WAREHOUSE` = `COMPUTE_WH`
   - `SF_PRIVATE_KEY_B64` = base64 PKCS#8 PEM
   - `SF_PRIVATE_KEY_PWD` = (optional, if key encrypted)
3. **Run manual dispatch** for yesterday:
   ```
   gh workflow run api-to-bronze-sync.yml -f endpoints=po,grn
   ```
4. **Verify BRONZE rows landed:**
   ```sql
   SELECT * FROM V2RETAIL.BRONZE.RFC_API_SYNC_LOG
   ORDER BY ENDED_AT DESC LIMIT 10;
   ```
5. **Enable schedule** by uncommenting the `schedule:` block in `api-to-bronze-sync.yml`.

## Backfill

```
gh workflow run api-to-bronze-sync.yml \
  -f date_from=2026-05-01 \
  -f date_to=2026-05-07 \
  -f endpoints=po,grn,vendors
```

Each endpoint enforces its own window cap server-side (PO 31d, GRN+Sales 7d, masters 31d).
For wider backfills, run the workflow multiple times.

## Isolation guarantees

- Writes ONLY to `BRONZE.RFC_API_*` tables. Never touches `BRONZE.ET_*` (Shubham's namespace).
- Does NOT read or modify `GOLD.RFC_MASTER`.
- Cron runs 1 hour after Shubham's `rfc-bronze-sync.yml` to avoid V2RfcTestPool collision.
- Uses `concurrency: api-to-bronze-sync` group — only one run at a time, even across dispatch + schedule.
