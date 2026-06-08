# Article Characteristic Patch API (`/api/article/patch`)

Sparse PATCH endpoint for SAP article master data attributes. Backed by ABAP FM `Z_ART_PATCH_RFC_V61` (multi-class routing).

**Status**: V61 LIVE DEV + QA (2026-06-05). PROD currently 403-blocked in controller.

## Endpoint

```
POST https://sap-api.v2retail.net/api/article/patch?env={dev|qa}
Content-Type: application/json
```

## Request body

```json
{
  "matnr": "1124025475",
  "changes": { "ATTR_NAME": "value", "ATTR_NAME_2": "value" },
  "testMode": true,
  "user": "DEVELOPER_NAME"
}
```

| Field      | Type    | Required | Description                                                   |
|------------|---------|----------|---------------------------------------------------------------|
| `matnr`    | string  | yes      | MATNR — leading zeros optional, padded server-side             |
| `changes`  | object  | yes      | Map of attribute name → new value. One PATCH per call.        |
| `testMode` | boolean | no       | `true` returns routing plan without commit. Default `false`.  |
| `user`     | string  | no       | Audit tag (any string). Defaults to `RFC_USER`.               |

## curl examples

**QA test mode (no write, returns routing plan)**:
```bash
curl -X POST 'https://sap-api.v2retail.net/api/article/patch?env=qa' \
  -H 'Content-Type: application/json' \
  -d '{"matnr":"1124025475","changes":{"M_FAB_MAIN_MVGR_1":"SLD"},"testMode":true,"user":"DEV"}'
```

**QA real write (commits AUSP/ZCT04, no rollback)**:
```bash
curl -X POST 'https://sap-api.v2retail.net/api/article/patch?env=qa' \
  -H 'Content-Type: application/json' \
  -d '{"matnr":"1124025475","changes":{"M_FAB_MAIN_MVGR_1":"SLD"},"testMode":false,"user":"DEV"}'
```

**Multi-attr in one call**:
```bash
curl -X POST 'https://sap-api.v2retail.net/api/article/patch?env=qa' \
  -H 'Content-Type: application/json' \
  -d '{
    "matnr": "1124025475",
    "changes": {
      "M_FAB_MAIN_MVGR_1": "SLD",
      "M_FAB_MAIN_MVGR_2": "DNM"
    },
    "testMode": false,
    "user": "DEV"
  }'
```

## Response

```json
{
  "Status": true,
  "Env": "qa",
  "Matnr": "1124025475",
  "TestMode": false,
  "ResultJson": "{\"matnr\":\"000000001124025475\",\"ok\":true,\"applied\":1,\"candidates\":[{\"class\":\"123020607\",\"klart\":\"026\",\"clint\":\"0000010927\"}],\"results\":[{\"fn\":\"M_FAB_MAIN_MVGR_1\",\"route\":\"AUSP\",\"klart\":\"026\",\"class\":\"123020607\",\"status\":\"APPLIED\"}]}"
}
```

`ResultJson` is a JSON-encoded inner payload:

| Field         | Description                                                           |
|---------------|-----------------------------------------------------------------------|
| `matnr`       | Padded MATNR as written to SAP                                        |
| `ok`          | `true` if all attrs applied successfully                              |
| `applied`     | Count of attrs committed (real-write mode only)                       |
| `candidates`  | All candidate classes found for this MATNR (KLART 001, 026, 300, ...) |
| `results[]`   | Per-attr outcome (real-write mode)                                    |
| `plan[]`      | Per-attr planned route (test mode)                                    |

## Route table

Each attribute is routed by V61 based on its location:

| Route          | When                                                  | Sink                                        |
|----------------|-------------------------------------------------------|---------------------------------------------|
| `AUSP`         | Attr ATINN present in any candidate class KSML        | `BAPI_OBJCL_CHANGE` (with grouping per class) |
| `ZCT04`        | Attr is a column on `ZCT04_CHARACTER`                 | Direct `MODIFY ZCT04_CHARACTER`             |
| `NOT_IN_CLASS` | Attr in CABN globally but not in any MATNR class      | Error, no write                             |
| `LOCKED`       | Attr in 16-field locked list                          | Error, no write                             |
| `UNKNOWN`      | Attr not found in ZCT04 nor CABN                      | Error, no write                             |
| `NONE`         | (returned alongside an error)                         | n/a                                         |

## Multi-class routing (V61 fix)

V61 enumerates **all** candidate classes for a MATNR:

1. KLART `001` (general material class) via `KSSK(OBJEK=MATNR)`
2. Any other KLART (e.g. `026` variant config) via `INOB(MATNR, OBTAB=MARA) → CUOBJ → KSSK(CUOBJ, KLART)`

Then per-attr scans every candidate class's KSML, picks the first hosting class, groups BAPI calls by `(CLASSTYPE, CLASS)`.

**Critical**: `BAPI_OBJCL_CHANGE` is always invoked with `OBJECTKEY = MATNR` even for KLART 026 — the BAPI resolves CUOBJ internally via INOB. Passing CUOBJ directly returns `CL/763 Object does not exist`.

**V4 supersedes**: V4 only scanned KLART 001, returning `NOT_IN_CLASS` for the 18 KLART 026 article-creation fields (`M_FAB_MAIN_MVGR_1`, etc).

## Locked fields (16, server-side reject)

`MATNR`, `COLOR`, `SIZE`, `MRP`, `COST`, `M_FAB_DIV`, `SEASON`, `M_MAIN_MVGR`, `MC_CD`, `SUB_DIV`, `VENDOR`, `BRAND`, `HSN_CODE`, `BODY_ART_NO`, `DSG_NO`, `PRICE_BAND_CATEGORY`.

## Allowed-values lookup (V5 endpoint)

For frontend dropdowns, V5 provides:

```
GET /api/article/allowed-values?atnam=M_FAB_MAIN_MVGR_1&matnr=1124025475&env=qa
```

Returns CAWN allowed-values + source tag (`CABN_IN_CLASS` / `CABN_GLOBAL_NOT_IN_CLASS` / `CABN_GLOBAL`).

## Sample workflow (test → apply)

```bash
# 1. Discover allowed values
curl -s 'https://sap-api.v2retail.net/api/article/allowed-values?atnam=M_FAB_MAIN_MVGR_1&matnr=1124025475&env=qa'

# 2. Test routing (no write)
curl -s -X POST 'https://sap-api.v2retail.net/api/article/patch?env=qa' \
  -H 'Content-Type: application/json' \
  -d '{"matnr":"1124025475","changes":{"M_FAB_MAIN_MVGR_1":"SLD"},"testMode":true,"user":"DEV"}'

# 3. Apply (real write)
curl -s -X POST 'https://sap-api.v2retail.net/api/article/patch?env=qa' \
  -H 'Content-Type: application/json' \
  -d '{"matnr":"1124025475","changes":{"M_FAB_MAIN_MVGR_1":"SLD"},"testMode":false,"user":"DEV"}'

# 4. Verify AUSP write
# (via universal-mcp tool: sap_read_table env=qa table=AUSP where="OBJEK='000000000008114524' AND ATINN='0000001257'")
```

## Environment routing

| `env=` | SAP host        | Behavior              |
|--------|------------------|-----------------------|
| `dev`  | `.174:210/SAP_ABAP` | DEV sandbox writes |
| `qa`   | `.179:600/POWERBI`  | QA writes           |
| `prod` | `.170:600/BATCHUSER` | **Currently 403-blocked** in controller `ResolveEnv()`. Lift after QA UAT. |

## Backing FM

- ABAP FM: `Z_ART_PATCH_RFC_V61` (FG `ZARTPV61FG1`, PNAME `SAPLZARTPV61FG1`)
- TR shipped to QA: `S4DK925666` (with 14 dead probe FGs — cosmetic RC 16 in tp log, keeper activated cleanly)
- Source location: SAP DEV system, include `LZARTPV61FG1U01` (417 lines)
- Build runbook: [`ZART_CHAR_PATCH_V61_RUNBOOK.md`](./ZART_CHAR_PATCH_V61_RUNBOOK.md)
- Lessons learned: [`LESSONS_LEARNED_2026_06_05.md`](./LESSONS_LEARNED_2026_06_05.md)
