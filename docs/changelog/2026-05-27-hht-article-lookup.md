# 2026-05-27 — HHT Article Lookup API live

## What shipped
- `POST /api/article-lookup` — single-call combined lookup for Article Type (L/RL/NL/C) + Article Size (BS/S/NORMAL).
- BRD: `BRD_API_Article_Type_Size.docx` (V09 → 0001 Article Putaway HHT screen).
- Controller: `Controllers/HHT/ArticleLookupController.cs`.
- Contract: `docs/HHT-ARTICLE-LOOKUP-API.md`.

## Data source
- DataV2 (Server 28) — `L_VAR_ARTICLE`, `ST_ART_DISCOUNT`, `SZ_GROUP_VAR_ARTICLE`.
- No SAP RFC dependency; impersonates `V2RD\akash.agarwal` then falls back to `V2ApiReader`.
- All 3 tables resolved in `dbo`; row counts: 3.93M / 22 active / 1.46M.

## Logic
- Article Type:
  1. Active discount in `ST_ART_DISCOUNT` for (`@store` OR `'All'`) at `@today` → `C`.
  2. Else `L_VAR_ARTICLE.L_STATUS` for (store, article) → `L` / `RL` / `NL`.
  3. Else default `NL`.
- Article Size:
  - `SZ_GROUP_VAR_ARTICLE.SIZE_GRP` for article. `BS`, `S` pass through; `N` → `NORMAL`.

## Deploy
- Commit `f34ad1f` → rebased `d6c91cd` → master.
- GHA `deploy-iis.yml` run `26494239592` — build + IIS .36 + V2RfcTestPool recycle + health ✓.

## Smoke (live)
| Case | Input | Output | ms |
|------|-------|--------|----|
| L  | HA11/1125011967001 | L  / S      | 189 |
| RL | HA10/1114091884004 | RL / NORMAL | 499 |
| NL | HB06/1123117012004 | NL / NORMAL | 307 |
| C  | HA11/1220006246001 | C  / NORMAL | 687 |
| BS | HA11/1130139636004 | L  / BS     | 623 |
| phantom | HA11/9999999999999 | NL / NORMAL | 1205 |
| missing fields | `{"store":"HA11"}` | HTTP 400 | — |
| non-numeric    | `{"article":"ABC"}` | HTTP 400 | — |
| malformed JSON | `not json`          | HTTP 400 | — |

## Open items (DBA / mobility)
- **DBA:** add covering indexes on Server 28 (write-perm denied for API service account):
  - `IX_LVA_STORE_ART` on `L_VAR_ARTICLE(STORE, ARTICLE_NUMBER) INCLUDE(L_STATUS)`
  - `IX_SGVA_ART` on `SZ_GROUP_VAR_ARTICLE(ARTICLE_NUMBER) INCLUDE(SIZE_GRP)`
  - Removes ~6 s cold-pool wedge; warm path already <1 s.
- **Mobility team:** integrate `/api/article-lookup` into V09 → 0001 Article Putaway screen.
- **Pilot:** one store per BRD §R1 before national rollout.
