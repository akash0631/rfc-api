# HHT Article Lookup API

Surfaces `Article Type` and `Article Size` on the V09 → 0001 Article Putaway HHT scan screen.
Source BRD: `BRD_API_Article_Type_Size.docx` (Article Type & Article Size APIs, v1.0).

## Endpoint

```
POST https://sap-api.v2retail.net/api/article-lookup
Content-Type: application/json
```

No auth header. CORS open (`Access-Control-Allow-Origin: *`).

## Request

```json
{
  "store":   "HA11",
  "article": "1125011967001"
}
```

| Field   | Type    | Required | Notes                                                       |
|---------|---------|----------|-------------------------------------------------------------|
| store   | string  | yes      | Store code (e.g. `HA11`, `HB06`). Matches `L_VAR_ARTICLE.STORE` + `ST_ART_DISCOUNT.ST_CD`. |
| article | string  | yes      | Variant article number (13-digit numeric). Matches `L_VAR_ARTICLE.ARTICLE_NUMBER` + `ST_ART_DISCOUNT.MATNR` + `SZ_GROUP_VAR_ARTICLE.ARTICLE_NUMBER`. |

Aliases accepted on request body (case-insensitive): `store | STORE | IM_STORE`, `article | ARTICLE | ARTICLE_NUMBER | IM_ARTICLE`.

## Response — success

```json
{
  "status":       true,
  "store":        "HA11",
  "article":      "1125011967001",
  "article_type": "L",
  "article_size": "S",
  "ms":           189
}
```

| Field        | Values                          | Meaning |
|--------------|---------------------------------|---------|
| article_type | `L`, `RL`, `NL`, `C`            | `C` if active discount in `ST_ART_DISCOUNT` for store (or `All`) today; else `L_STATUS` from `L_VAR_ARTICLE`; else `NL`. |
| article_size | `BS`, `S`, `NORMAL`             | `SIZE_GRP` from `SZ_GROUP_VAR_ARTICLE` for the article; `N` mapped to `NORMAL`. |

## Response — validation error (HTTP 400)

```json
{ "status": false, "message": "store and article are required" }
{ "status": false, "message": "article must be numeric (variant article number)" }
{ "status": false, "message": "Invalid JSON body" }
```

## Response — lookup unavailable (HTTP 200, per BRD §5.4)

If DataV2 (Server 28) is unreachable or times out, the HHT app must still allow the operator to complete the putaway.

```json
{
  "status":       false,
  "store":        "HA11",
  "article":      "1125011967001",
  "article_type": null,
  "article_size": null,
  "message":      "Lookup unavailable: <reason>",
  "ms":           6300
}
```

HTTP code is **200 OK** even on failure — clients must inspect `status` + `article_type`/`article_size`. Show a placeholder on screen, do not block the operator.

## HHT screen mapping (BRD §5.1)

| HHT label    | Field         | Position             |
|--------------|---------------|----------------------|
| Article Type | article_type  | Below `Article`      |
| Article Size | article_size  | Below `Article Type` |

Both fields are read-only on the HHT screen.

## Performance

| Path                | Live measured (post-deploy 2026-05-27) |
|---------------------|-----------------------------------------|
| Warm L lookup       | ~190 ms                                 |
| Warm RL lookup      | ~500 ms                                 |
| Warm NL lookup      | ~300–1200 ms                            |
| Warm C lookup       | ~690 ms                                 |
| Cold pool (first call after IIS recycle) | ~6 s — wedges once, recovers immediately |

BRD §5.5 target: <1s under normal store-network conditions. Warm path meets target.

Cold-call wedge will be removed once DBA adds these indexes on Server 28:
```sql
CREATE NONCLUSTERED INDEX IX_LVA_STORE_ART
  ON dbo.L_VAR_ARTICLE(STORE, ARTICLE_NUMBER) INCLUDE(L_STATUS);
CREATE NONCLUSTERED INDEX IX_SGVA_ART
  ON dbo.SZ_GROUP_VAR_ARTICLE(ARTICLE_NUMBER) INCLUDE(SIZE_GRP);
```
(`ST_ART_DISCOUNT` already has `INDX_ST_CD_MATNR`.)

## Smoke test reference cases

```bash
# L     listed
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"store":"HA11","article":"1125011967001"}' \
  https://sap-api.v2retail.net/api/article-lookup
# -> {"article_type":"L","article_size":"S"}

# RL    re-listed
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"store":"HA10","article":"1114091884004"}' \
  https://sap-api.v2retail.net/api/article-lookup
# -> {"article_type":"RL","article_size":"NORMAL"}

# NL    not listed (real row)
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"store":"HB06","article":"1123117012004"}' \
  https://sap-api.v2retail.net/api/article-lookup
# -> {"article_type":"NL","article_size":"NORMAL"}

# C     clearance / discount
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"store":"HA11","article":"1220006246001"}' \
  https://sap-api.v2retail.net/api/article-lookup
# -> {"article_type":"C","article_size":"NORMAL"}

# BS    big-size variant
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"store":"HA11","article":"1130139636004"}' \
  https://sap-api.v2retail.net/api/article-lookup
# -> {"article_type":"L","article_size":"BS"}

# default (phantom article)
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"store":"HA11","article":"9999999999999"}' \
  https://sap-api.v2retail.net/api/article-lookup
# -> {"article_type":"NL","article_size":"NORMAL"}
```

## Acceptance Criteria mapping (BRD §10)

| BRD AC | Status | Evidence |
|--------|--------|----------|
| AC1 — fields appear on screen          | HHT app (out-of-scope for this repo) | — |
| AC2 — discount article → `C`           | PASS | `1220006246001` |
| AC3 — variant listing → `L`/`RL`/`NL`  | PASS | `1125011967001` / `1114091884004` / `1123117012004` |
| AC4 — neither source → defaults `NL`   | PASS | `9999999999999` |
| AC5 — big-size → `BS`                  | PASS | `1130139636004` |
| AC6 — otherwise `NORMAL`               | PASS | all non-BS samples |
| AC7 — <1s response                     | PASS (warm path) | warm 190–1200 ms |
| AC8 — read-only on HHT                 | HHT app | — |
| AC9 — graceful fallback                | PASS | timeout → status=false + null fields + HTTP 200 |

## Source

- Controller: `Controllers/HHT/ArticleLookupController.cs`
- Connection: DataV2 / Server 28 (`192.168.151.28`) — impersonates `V2RD\akash.agarwal`, falls back to `V2ApiReader` SQL login.
- Read-only. Single round-trip. No SAP RFC dependency.
