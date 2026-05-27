# PO + GRN Read APIs — direct SAP → Lovable / Snowflake

> **Status:** built 2026-05-27 · **Owner:** Akash · **Repo:** `akash0631/rfc-api`

Dedicated read endpoints that bypass the legacy DataV2 → Snowflake mirror and pull purchase-order + goods-receipt data straight from SAP S/4HANA. Designed for Lovable apps that need fresh PO/GRN data without depending on the nightly ETL.

## Why these exist

| Old path | New path |
|---|---|
| SAP → DataV2 SQL Server 28 ETL → mirrored to Snowflake → consumed by Lovable | SAP → `/api/po` or `/api/grn` → Lovable (and optionally Snowflake BRONZE) |

Removes:
- 7-8 day BRONZE staleness (legacy clone-once, no daily delta)
- Dependence on `V2DC-ADDVERB` server uptime
- Dependence on `RFC_MASTER` catalog hydration (`MAX_WINDOW_DAYS` migration was blocking the generic `/api/execute/{rfc}/sync` path)

## Endpoints

### `POST /api/po`

Wraps SAP RFC `ZMM_PO_DETAILS` (FMODE='R', verified live on DEV / QA / PROD).

| Field | Type | Required | Notes |
|---|---|---|---|
| `DateFrom` | string | ✅ | `YYYY-MM-DD`. Maps to SAP `IT_CREATED_LOW` (EKKO-ERDAT) |
| `DateTo` | string | optional | `YYYY-MM-DD`. Defaults to `DateFrom`. Maps to `IT_CREATED_HIGH` |
| `Plant` | string | optional | Client-side filter on `PLANT` (e.g. `DH24`, `HO08`) |
| `Vendor` | string | optional | Client-side filter on `SUPPLIER` (leading zeros stripped before compare) |
| `Limit` | int | optional | Cap on rows returned. Default `50000` |

Query: `?env=dev|qa|prod` (default `prod`)

### `POST /api/grn`

Wraps SAP RFC `ZPBI_GRC_DETAILS` (FMODE='R', verified live).

> ⚠ The FM streams the full `ET_GRC_DATA` table — there is no SAP-side date param. We filter client-side. Use `Plant` or `Vendor` to narrow before calling, especially for Lovable UX.

| Field | Type | Required | Notes |
|---|---|---|---|
| `DateFrom` | string | optional | `YYYY-MM-DD`. Filter on `BUDAT` (posting date) |
| `DateTo` | string | optional | `YYYY-MM-DD` |
| `Plant` | string | optional | Filter on `WERKS` |
| `Vendor` | string | optional | Filter on `LIFNR` (leading zeros stripped) |
| `MovementType` | string | optional | Filter on `BWART` (101 = GR, 102 = GR reverse) |
| `Limit` | int | optional | Default `100000` |

Query: `?env=dev|qa|prod` (default `prod`)

## Auth

Header `X-RFC-Key: v2-rfc-proxy-2026` on every call (same shared secret as `/api/rfc/proxy`, `/api/execute/*`, etc.).

## Examples

```bash
# Yesterday's PO headers from PROD
curl -X POST 'https://sap-api.v2retail.net/api/po?env=prod' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -H 'Content-Type: application/json' \
  -d '{"DateFrom":"2026-05-26"}'

# Last 7 days PO for one plant on QA
curl -X POST 'https://sap-api.v2retail.net/api/po?env=qa' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -H 'Content-Type: application/json' \
  -d '{"DateFrom":"2026-05-20","DateTo":"2026-05-26","Plant":"DH24"}'

# Today's goods receipts (movement type 101) for one plant
curl -X POST 'https://sap-api.v2retail.net/api/grn?env=prod' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -H 'Content-Type: application/json' \
  -d '{"DateFrom":"2026-05-27","DateTo":"2026-05-27","Plant":"DH24","MovementType":"101"}'
```

## Response shape

`POST /api/po`:

```json
{
  "Success": true,
  "Source": "ZMM_PO_DETAILS",
  "Env": "prod",
  "DateFrom": "2026-05-26",
  "DateTo": "2026-05-26",
  "SapMessage": null,
  "TotalRowsFromSap": 12,
  "RowCount": 12,
  "Rows": [
    { "PurchasingDoc": "4500012345", "PoType": "NB", "CreatedOn": "2026-05-26",
      "CreatedBy": "SAP_MM", "Supplier": "0000200001", "NetValue": "1440.00",
      "PoQuantity": "12.000", "Plant": "DH24" }
  ]
}
```

`POST /api/grn`:

```json
{
  "Success": true,
  "Source": "ZPBI_GRC_DETAILS",
  "Env": "prod",
  "DateFrom": "2026-05-27",
  "DateTo": "2026-05-27",
  "Plant": "DH24",
  "Vendor": null,
  "MovementType": "101",
  "TotalRowsFromSap": 41208,
  "RowCount": 86,
  "Rows": [
    { "MaterialDoc": "5000123456", "Year": "2026", "Line": "0001",
      "PostingDate": "2026-05-27", "MovementType": "101",
      "Material": "000000001110000003", "Plant": "DH24", "Batch": "",
      "StorageLocation": "0032", "Vendor": "0000200001", "DebitCredit": "S",
      "Currency": "INR", "Amount": "1200.00", "Quantity": "10.000",
      "Uom": "EA", "MaterialGroup": "110101001", "OldMaterial": "",
      "ArticleType": "02", "PpkQty": "1.000" }
  ]
}
```

## Field mapping (SAP → camelCase)

### PO (`/api/po`)

| SAP (`IT_FINAL`) | API field | Source |
|---|---|---|
| `PURCHASING_DOC` | `PurchasingDoc` | EKKO-EBELN |
| `PO_TYPE` | `PoType` | EKKO-BSART |
| `CREATED_ON` | `CreatedOn` | EKKO-AEDAT |
| `CREATED_BY` | `CreatedBy` | EKKO-ERNAM |
| `SUPPLIER` | `Supplier` | EKKO-LIFNR |
| `NET_VALUE` | `NetValue` | aggregated EKPO-NETWR |
| `PO_QUANITY` *(sic in SAP)* | `PoQuantity` | aggregated EKPO-MENGE |
| `PLANT` | `Plant` | EKPO-WERKS |

### GRN (`/api/grn`)

| SAP (`ET_GRC_DATA`) | API field | Source |
|---|---|---|
| `MBLNR` | `MaterialDoc` | MKPF-MBLNR |
| `MJAHR` | `Year` | MKPF-MJAHR |
| `ZEILE` | `Line` | MSEG-ZEILE |
| `BUDAT` | `PostingDate` | MKPF-BUDAT |
| `BWART` | `MovementType` | MSEG-BWART |
| `MATNR` | `Material` | MSEG-MATNR |
| `WERKS` | `Plant` | MSEG-WERKS |
| `CHARG` | `Batch` | MSEG-CHARG |
| `LGORT` | `StorageLocation` | MSEG-LGORT |
| `LIFNR` | `Vendor` | MSEG-LIFNR |
| `SHKZG` | `DebitCredit` | `S`=receipt, `H`=reversal |
| `WAERS` | `Currency` | MKPF-WAERS |
| `DMBTR` | `Amount` | MSEG-DMBTR (rupees) |
| `MENGE` | `Quantity` | MSEG-MENGE |
| `MEINS` | `Uom` | MSEG-MEINS |
| `MATKL` | `MaterialGroup` | MARA-MATKL |
| `BISMT` | `OldMaterial` | MARA-BISMT |
| `ATTYP` | `ArticleType` | MARA-ATTYP |
| `PPK_QTY` | `PpkQty` | derived |

## Caveats

1. **PO `SapMessage`**: SAP FM `ZMM_PO_DETAILS` returns `EX_RETURN.MESSAGE='No Data Found'` even when `IT_FINAL` contains rows. We only mark `Success=false` when both `EX_RETURN.TYPE='E'` AND `RowCount==0`. The `SapMessage` field is surfaced for diagnostics but should not be treated as an error if `Rows.length > 0`.
2. **GRN volume**: `ZPBI_GRC_DETAILS` returns full history, not delta. Always pass `DateFrom`/`DateTo` + `Plant` for production traffic. Without filters, expect 10-30s response times and large payloads.
3. **Date format**: API accepts `YYYY-MM-DD` (ISO). Internally converted to SAP `YYYYMMDD`. Either format is accepted.
4. **All numeric fields are strings**: SAP returns DECIMAL fields as zero-padded strings (`"1440.00"`, `"12.000"`). Caller must `parseFloat()` before arithmetic.
5. **Leading-zero vendor codes**: SAP stores `LIFNR` as 10-char zero-padded (`0000200001`). The filter strips leading zeros so callers can pass `200001` or the full form interchangeably.

## Operations

- Deploys via `.github/workflows/deploy-iis.yml` on push to `master` → IIS box `V2DC-ADDVERB` → CF Tunnel → `sap-api.v2retail.net`
- Health: `GET /api/rfc/proxy/health` (shared SAP NCo pool)
- Pool wedge runbook: `docs/RFC-POOL-WEDGE-RUNBOOK.md`
- Snowflake landing: not wired yet. The endpoints currently return JSON only. To land in BRONZE, post the result via existing Snowflake ingestion (Shubham TODO) or call `/api/execute/ZMM_PO_DETAILS/sync` instead once the `MAX_WINDOW_DAYS` migration is applied.

## Change log

| Date | Change | By |
|---|---|---|
| 2026-05-27 | Initial build. `/api/po` (ZMM_PO_DETAILS) + `/api/grn` (ZPBI_GRC_DETAILS) | Akash |
