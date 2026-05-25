# V2 PO Creator — API Mapping

> Source: Google Drive — "API Data Warehouse Mapping for PO Creator Application"
> Upstream API Base: `http://192.168.151.77:8090/api` | Version: 1.5.56
> SAP-side endpoint: `POST https://sap-api.v2retail.net/api/ZMM_PO_CREATION_RFC` (bound to PROD as of 2026-05-25)

## Core API Entities

### 1. Product_Master
- **Fields:** ARTICLE NUMBER, COLOR CODE, SIZE, SEGMENT, DIVISION, SUB DIVISION, MAJ CATEGORY, MC CODE, MC DESCRIPTION, MRP, ARTICLE DESCRIPTION, VENDOR CODE, ARTICLE STATUS
- **Usage:** Product selection + validation for PO creation

### 2. ET_Supplier_Master
- **Fields:** LIFNR (Vendor ID), NAME1, KTOKK (Type), STRAS (Address), ORT01 (City), TELF1/2 (Phone), SMTP_ADDR (Email), ZTERM (Payment Terms), SPERR (Status)

### 3. PO_Pending
- **Fields:** PO NUMBER, STORE CODE, Article Number, DOC_DT, EX_FAC_DT, GRC_DEL_DT, PO STATS, Actual PO Quantity/Value, PENDING PO QUANTITY/VALUE, VENDOR_NAME, NET_PRICE
- **Multi-fabric:** FAB1–FAB5 vendor and PO fields

### 4. GRC_Report (Goods Received)
- **Fields:** ARTICLE DOCUMENT NUMBER, ARTICLE NUMBER, STORE CODE, VENDOR CODE, GRC_VALUE, GRC_QTY, POSTING DATE, MC CODE
- **KPIs:** On-time delivery rate, quantity accuracy, vendor reliability score

### 5. PUR_BGT_GEN_ART_PR_Q_PLAN
- Budget and quantity planning data

## PO Creation Workflow

1. Product Selection (Product_Master + filters)
2. Vendor Selection (ET_Supplier_Master + performance from GRC)
3. Delivery Details (store, dates)
4. Review & Confirm (cost calcs, anomaly alerts)
5. Submit → `POST /api/ZMM_PO_CREATION_RFC` → SAP PROD

## Advanced Features

- AI quantity suggestions based on GRC history + seasonal patterns
- Anomaly detection (high qty slow movers, missing top sellers, vendor issues)
- Vendor performance scoring (fill rate, on-time delivery, quantity accuracy)
- Multi-vendor fabric sourcing (FAB1–FAB5)

## Tech Stack

- React + TypeScript on Lovable
- API Client: Axios with OData params (`$select`, `$filter`, `$orderby`, `$first`, `$after`)
- Auth: `X-MS-API-ROLE` + `Authorization` headers
- Caching: Products (1hr), Vendors (30min), Performance (15min)

## SAP-side Contract — `POST /api/ZMM_PO_CREATION_RFC`

**Controller:** [`Controllers/MM/ZMM_PO_CREATION_RFCController.cs`](../Controllers/MM/ZMM_PO_CREATION_RFCController.cs)
**SAP FM:** `ZMM_PO_CREATION_RFC` in FG `ZPO_CREATION_FG` (PROD client 600)
**Concurrency:** process-wide SemaphoreSlim, 60s timeout — concurrent calls serialized (FM uses memory IDs `Z_PO_UPLOAD`, `Z_PO_TAX`, `Z_IT_FINAL`).

### Request

```json
{
  "IV_VENDOR":   "100000",
  "IV_DOC_TYPE": "NB",
  "IT_ITEMS": [
    {
      "MATERIAL":    "<MATNR external, e.g. 4030000123>",
      "QTY":         "10",
      "NET_PRICE":   "499.00",
      "DEL_DATE":    "20260615",
      "PLANT":       "1001",
      "STORAGE_LOC": "0001"
    }
  ]
}
```

| Field | SAP type | Notes |
|-------|----------|-------|
| `IV_VENDOR` | LIFNR | external format ok — FM zero-pads internally |
| `IV_DOC_TYPE` | BSART | defaults to `NB` if blank |
| `IT_ITEMS[].MATERIAL` | MATNR | external — FM applies `CONVERSION_EXIT_MATN1_INPUT` |
| `IT_ITEMS[].QTY` | BSTMG | **must be `QTY` (not `QUANTITY`)** |
| `IT_ITEMS[].NET_PRICE` | BPREI | |
| `IT_ITEMS[].DEL_DATE` | EINDT | `YYYYMMDD` |
| `IT_ITEMS[].PLANT` | EWERK | 4-char |
| `IT_ITEMS[].STORAGE_LOC` | LGORT_D | 4-char |

### Response — success

```json
{ "Status": true, "Message": "PO 4500001234 created successfully", "PoNumber": "4500001234" }
```

### Response — error

```json
{ "Status": false, "Message": "<SAP message>", "PoNumber": "" }
```

### Common error fingerprints

| Message | Cause |
|---------|-------|
| `" Please provide all mandatory inputs"` (lowercase 'P') | `IT_ITEMS` arrived empty on PROD FM — check payload field names (`QTY` not `QUANTITY`) |
| `" PLEASE PROVIDE ALL MANDATORY INPUTS"` (uppercase) | Same, but controller is still pointed at QA |
| `"Plant 1000 not defined"` | Reached FM body, BAPI validation failed on plant — check `EWERK` |
| `"no SAP ErrInfo available"` | Pre-2026-05-08 ALPHA bug; should not recur — see [SAP-RFC-ALPHA-CONVERSION.md](./SAP-RFC-ALPHA-CONVERSION.md) |

## History

### Controller env binding flip (2026-05-25)

Controller was hardcoded to QA (`rfcConfigparametersquality()`) until 2026-05-25. Flipped to PROD (`rfcConfigparametersproduction()` → .170 / Client 600 / PRD) on master `561c3f8`. CI run `26393122103` deployed to .36 IIS. Routing fingerprint verified via ELSE-branch casing. End-to-end PO create pending app-team smoke. See [changelog/2026-05-25-zmm-po-creation-prod-flip.md](./changelog/2026-05-25-zmm-po-creation-prod-flip.md).

### ALPHA / MATN1 fix (2026-05-08)

Original PROD failure traced to missing internal-format conversion before the EINA/EINE SELECT. Five-line fix landed in FM. Full lesson: [SAP-RFC-ALPHA-CONVERSION.md](./SAP-RFC-ALPHA-CONVERSION.md).
