# eCommerce Order POST — Vendor-Segregated APIs

> **Status:** LIVE on dev, qa and prod · **Owner:** Akash · **Repo:** `akash0631/rfc-api`
> **Controller:** `Controllers/NSO/ZecommOrderPostController.cs`
> **Last verified:** 2026-07-27 (end-to-end on QA, read-back confirmed)

Two dedicated endpoints that post a full eCommerce order (header + address +
line items) into SAP via RFC `ZECOMM_ORDER_POST_RFC`. The **only** difference
between them is the vendor code the server stamps onto the order header — so
orders can be reported and tracked **vendor-wise**.

| Endpoint | Forces `ET_HEAD.VENDOR_NAME` = |
|---|---|
| `POST /api/ZECOMM_ORDER_POST_UT` | `UT` |
| `POST /api/ZECOMM_ORDER_POST_GH` | `GH` |

Both accept an **identical** request body. Reporting is a filter on
`ZECOMM_ORD_HEAD.VENDOR_NAME`.

---

## ⚠️ Two things every integrator must know

### 1. `HTTP 200` does NOT mean the order was saved

The controller returns `HttpStatusCode.OK` unconditionally once the RFC call
completes. Business rejections come back as **`200` with `Status: "E"`**.

```json
HTTP 200
{"Status":"E","Message":"Address data missing","Vendor":"UT","Env":"qa", ...}
```

**Branch on `Status == "S"`, never on the HTTP status code alone.** A caller
checking only the HTTP code will log rejected orders as successfully saved.

Only `Status: "S"` means committed. `"E"` / `"A"` mean nothing was written.

### 2. The controller's required-field check is not SAP's

The controller validates only three fields and returns `400` if they are
missing. The FM enforces considerably more and reports via `Status: "E"`.

**True minimum for a successful post** — verified on QA 2026-07-27:

| Payload | Result |
|---|---|
| Header with the 3 controller-mandatory fields only | `200` `Status:"E"` — `Address data missing` |
| `ET_ADD: [{}]` (empty) | `200` `Status:"E"` — `Address data missing` |
| `ET_ADD` populated, no items | `200` `Status:"E"` — `Item data missing` |
| Header + `ET_ADD` + at least one `ET_ITEM` | `200` `Status:"S"` ← **true minimum** |
| Duplicate `SALES_ORDER_CODE` | `200` `Status:"E"` — `Error saving Header` |

**`ET_ADD` is mandatory in practice**, despite being nullable on the request
model. Same for at least one line item.

**Retrying is safe.** A rejected post writes no header row and no orphan item
rows — verified by read-back of `ZECOMM_ORD_ITEM` after a duplicate-code
attempt (exactly one item row survived, from the original successful post).

---

## Request body — both shapes accepted

`ET_HEAD` and `ET_ADD` accept **either a JSON object or a single-element
array**. `ET_ITEM` is always an array.

```jsonc
// Both of these are valid and equivalent:
"ET_HEAD": { "SALES_ORDER_CODE": "SO-1001", ... }
"ET_HEAD": [ { "SALES_ORDER_CODE": "SO-1001", ... } ]
```

**Why both:** at the RFC layer `ET_HEAD` / `ET_ADD` genuinely are tables
(`ZECOMM_ORD_HEAD_TT` / `ZECOMM_ORD_ADD_TT`), so raw-RFC callers legitimately
send arrays. The FM does `READ TABLE ... INDEX 1` and only ever consumes one
row, so the dedicated routes model them as single objects.
`SingleOrFirstOfArrayConverter<T>` (`Controllers/NSO/`) collapses an array to
its first element, making both shapes work.

> **No batch posting.** A multi-row array silently takes row 1 and returns
> `Status:"S"`. Rows 2+ are discarded without warning — the same behaviour the
> FM has always had. One order per call.

JSON property order is not significant.

### `ET_HEAD` — order header (`ZECOMM_ORD_HEAD_STR`)

| Field | SAP type | Notes |
|---|---|---|
| `SALES_ORDER_CODE` | CHAR45 | **Controller-required.** Key |
| `DISPLAY_ORDER_CODE` | CHAR45 | **Controller-required.** Key |
| `ST_CD` | CHAR4 | **Controller-required.** Store code, key. e.g. `HD24` |
| `DISPLAY_ORDER_DATE` | DATS8 | `yyyyMMdd` |
| `DISPLAY_ORDER_TIME` | TIMS6 | `HHmmss` |
| `CUSTOMER_CODE` | CHAR20 | |
| `CUSTOMER_NAME` | CHAR100 | |
| `CUSTOMER_GST_NO` | CHAR16 | |
| `CHANNEL` | CHAR6 | e.g. `WEB`, `APP` |
| `NOTIFICATION_EMAIL` | CHAR100 | |
| `NOTIFICATION_MOBILE` | CHAR20 | |
| `CASH_ON_DELIVERY` | CHAR3 | `YES` / `NO`. **Not** the legacy `X` flag |
| `PAYMENT_INSTRUMENT` | CHAR20 | |
| `SHIPPING_CHARGES` | CURR 15.2 | Number or quoted number |
| `GIFTWRAP_CHARGES` | CURR 15.2 | Number or quoted number |
| `COD_CHARGES` | CURR 15.2 | Number or quoted number |
| `PICK_FROM_STORE` | CHAR4 | Store code when set, blank otherwise |
| `PREIMIMIM_DELIVERY` | CHAR20 | (SAP field name as-is) |
| `CUSTOM_FIELD_VALUES2` | CHAR45 | |
| `CUSTOM_FIELD_NAME3` / `CUSTOM_FIELD_VALUES3` | CHAR45 | |
| ~~`VENDOR_NAME`~~ | CHAR30 | **Not accepted** — forced to `UT`/`GH` server-side |

### `ET_ADD` — bill-to / ship-to (`ZECOMM_ORD_ADD_STR`)

Nullable on the model but **required in practice** — see the warning above.

Bill-to: `BILL_TO_NAME1`, `BILL_TO_NAME2`, `BILL_TO_ADDRESSLINE1`,
`BILL_TO_ADDRESSLINE2`, `BILL_TO_LATITUDE`, `BILL_TO_LONGITUDE`,
`BILL_TO_CITY`, `BILL_TO_STATE`, `BILL_TO_COUNTRY`, `BILL_TO_PINCODE`
(NUMC10), `BILL_TO_PHONE` (NUMC15), `BILL_TO_EMAIL`.

Ship-to: `SHIP_TO_NAME`, `SHIP_TO_NAME1`, `SHIP_TO_ADDRESSLINE1`,
`SHIP_TO_ADDRESSLINE2`, `SHIP_TO_LATITUDE`, `SHIP_TO_LONGITUDE`,
`SHIP_TO_CITY`, `SHIP_TO_STATE`, `SHIP_TO_COUNTRY`, `SHIP_TO_PINCODE`
(NUMC10), `SHIP_TO_PHONE` (NUMC15), `SHIP_TO_EMAIL`.

> NUMC fields (`*_PINCODE`, `*_PHONE`) accept digit strings only.
> There are **no** `CUSTOM_FIELD_*` fields on the address model — any sent are
> silently ignored (they appear in some circulating vendor samples).

### `ET_ITEM[]` — line items (`ZECOMM_ORD_ITEM_STR`)

At least one required in practice.

| Field | SAP type | Notes |
|---|---|---|
| `ITEM_NO` | NUMC2 | **Zero-pad**: `"01"`, `"02"`. Max 99 lines |
| `ITEM_SKU` | CHAR18 | SKU / EAN |
| `ST_CD` | CHAR4 | Must match the header store code |
| `MRP` | CURR 13.2 | Number or quoted number |
| `SALE_VALUE` / `DISCOUNT` / `SHIPPING_CHARGES` | CURR 15.2 | Number or quoted number |
| `CGST_RATE` / `SGST_RATE` / `IGST_RATE` | CHAR5 | **Strings** — `"9.00"`, `"2.5"`, `"0"` |
| `CGST_VALUE` / `SGST_VALUE` / `IGST_VALUE` | CURR 13.2 | Number or quoted number |
| `CUSTOM_FIELD_NAME1..3` / `CUSTOM_FIELD_VALUES1..3` | CHAR50 | Optional |

> The rate/value split is deliberate: the three `*_RATE` fields are **text** in
> SAP, the three `*_VALUE` fields are decimals. Vendor `VENDOR_NAME` is left
> blank on items by design — the stamp is header-only.

---

## Auth & environment

- **Auth:** none. No `X-RFC-Key`, no IP allow-list. See [Hardening](#hardening).
- **Environment:** `?env=dev|qa|prod` (default `dev`). One deployed binary —
  the query parameter only switches the SAP destination.

`ZECOMM_ORDER_POST_RFC`, `ZECOMM_ORD_HEAD`, `ZECOMM_ORD_ITEM` and `ZECOMM_ADD`
all exist and are active on **all three** SAP systems.

---

## Response

```json
{
  "Status": "S",
  "Message": "Order SO-1001 saved successfully",
  "Vendor": "UT",
  "Env": "qa",
  "Data": {
    "EX_RETURN": { "TYPE": "S", "ID": "", "NUMBER": "000",
                   "MESSAGE": "Order SO-1001 saved successfully" }
  }
}
```

`Vendor` echoes the forced vendor code; `Env` echoes the resolved environment.
Check both to confirm the request went where you intended.

### Error catalogue

| HTTP | Status | Message | Cause |
|---|---|---|---|
| 400 | `E` | `ET_HEAD is required` | `ET_HEAD` missing, `null`, or `[]` |
| 400 | `E` | `ET_HEAD.SALES_ORDER_CODE, DISPLAY_ORDER_CODE and ST_CD are required` | One of the three is blank |
| 200 | `E` | `Address data missing` | `ET_ADD` absent or empty |
| 200 | `E` | `Item data missing` | No line items |
| 200 | `E` | `Error saving Header` | Duplicate `SALES_ORDER_CODE` |
| 500 | `E` | `ABAP: …` | `RfcAbapException` |
| 500 | `E` | `RFC Comm: …` | `RfcCommunicationException` |

---

## Example

```bash
curl -X POST "https://sap-api.v2retail.net/api/ZECOMM_ORDER_POST_UT?env=qa" \
  -H "Content-Type: application/json" \
  -d '{
    "ET_HEAD": {
      "SALES_ORDER_CODE": "SO-1001", "DISPLAY_ORDER_CODE": "ORD-2026-1001",
      "ST_CD": "HD24", "DISPLAY_ORDER_DATE": "20260727", "DISPLAY_ORDER_TIME": "143000",
      "CUSTOMER_CODE": "CUST00123", "CUSTOMER_NAME": "John Doe",
      "CHANNEL": "WEB", "NOTIFICATION_MOBILE": "9876543210",
      "CASH_ON_DELIVERY": "YES", "PAYMENT_INSTRUMENT": "COD",
      "SHIPPING_CHARGES": "50.00", "GIFTWRAP_CHARGES": "0.00", "COD_CHARGES": "25.00"
    },
    "ET_ADD": {
      "BILL_TO_NAME1": "John Doe", "BILL_TO_ADDRESSLINE1": "12 MG Road",
      "BILL_TO_CITY": "Bengaluru", "BILL_TO_STATE": "KA", "BILL_TO_COUNTRY": "IN",
      "BILL_TO_PINCODE": "560001", "BILL_TO_PHONE": "9876543210",
      "SHIP_TO_NAME": "John Doe", "SHIP_TO_ADDRESSLINE1": "12 MG Road",
      "SHIP_TO_CITY": "Bengaluru", "SHIP_TO_STATE": "KA", "SHIP_TO_COUNTRY": "IN",
      "SHIP_TO_PINCODE": "560001", "SHIP_TO_PHONE": "9876543210"
    },
    "ET_ITEM": [
      { "ITEM_NO": "01", "ITEM_SKU": "SKU-A-001", "ST_CD": "HD24",
        "MRP": "1200.00", "SALE_VALUE": "999.00", "DISCOUNT": "201.00",
        "CGST_RATE": "9.00", "SGST_RATE": "9.00", "IGST_RATE": "0.00",
        "CGST_VALUE": "89.91", "SGST_VALUE": "89.91", "IGST_VALUE": "0.00" }
    ]
  }'
```

Swap `?env=qa` for `dev` or `prod`. Wrapping `ET_HEAD` / `ET_ADD` in `[ ]`
works identically.

---

## ⚠️ Do not confuse with the legacy endpoint

A different, older application is still reachable at:

```
https://routemaster.v2retail.com:9010/api/ZECOMM_ORDER_POST_RFC
```

Probed 2026-07-27:

- `routemaster.v2retail.com` resolves to **192.168.151.36** — the same host as
  this API, on a different port and a **separate application**
- Its response contract differs: `{"Status": false, "Message": "..."}` —
  `Status` is a **boolean**, not the `"S"`/`"E"` string this API returns
- `/api/ZECOMM_ORDER_POST_UT` returns **404** there — the legacy app has **no
  vendor segregation**, so orders posted to it carry **no `VENDOR_NAME`**
- Its **TLS certificate is revoked** (`CRYPT_E_REVOKED`). Strict clients fail
  the handshake; lenient ones still connect

Vendor-supplied curl samples referencing this host are targeting the legacy
app. Orders sent there will not appear in vendor-wise reporting. Owner and
decommission plan **unknown — needs an owner**.

---

## Underlying RFC

`ZECOMM_ORDER_POST_RFC` — writes `ZECOMM_ORD_HEAD`, `ZECOMM_ADD`,
`ZECOMM_ORD_ITEM`. Function group `SAPLZECOMM_ORDER_FG`.

| Parameter | Direction | Type |
|---|---|---|
| `ET_HEAD` | IMPORT (table) | `ZECOMM_ORD_HEAD_TT` → `ZECOMM_ORD_HEAD_STR` |
| `ET_ADD` | IMPORT (table) | `ZECOMM_ORD_ADD_TT` → `ZECOMM_ORD_ADD_STR` |
| `ET_ITEM` | TABLES | `ZECOMM_ORD_ITEM_TT` → `ZECOMM_ORD_ITEM_STR` |
| `EX_RETURN` | EXPORT | `BAPIRET2` |

The FM **commits internally** (`COMMIT WORK AND WAIT`) — no
`BAPI_TRANSACTION_COMMIT` from the wrapper. It stamps `UNAME` / `UDATE` /
`UZEIT` itself. There is **no test-run flag**: any successful call is a real,
committed order.

---

## Implementation notes

- One controller, two `[Route]` actions (`PostUt`, `PostGh`) calling a shared
  private `PostOrder(request, vendorCode)`. Vendor code is the only
  per-endpoint difference — the ET_HEAD/ET_ADD/ET_ITEM mapping lives in one
  place.
- `SingleOrFirstOfArrayConverter<T>` sets `FloatParseHandling.Decimal` around
  `JToken.Load` and restores it in a `finally`. Without this, buffering through
  `JToken` parses bare JSON numbers as `Double` and silently truncates
  precision before it reaches the `decimal?` properties. Do not remove.
- Picked up by the `Controllers\**\*.cs` glob in
  `Vendor_SRM_Routing_Application.csproj` — no `.csproj` edit needed.
- Env switch reuses `BaseController.rfcConfigparameters()` /
  `rfcConfigparametersquality()` / `rfcConfigparametersproduction()`.

---

## Hardening

**Open.** These routes accept **unauthenticated writes into SAP PROD** and are
in live use by an external vendor. Adding `X-RFC-Key` enforcement or an IP
allow-list is a **breaking change for current callers** — the key must be
distributed to both vendors and cut over in a coordinated window, not enabled
unilaterally.

Also consider whether `?env=prod` should be restricted to specific callers.

---

## Verification log

**2026-07-16** — RFC exercised on DEV via `/api/rfc/proxy` before the routes
existed. 4 orders (2 UT, 2 GH), all `S`, `VENDOR_NAME` persisted.

**2026-07-21** — Routes deployed. GH verified end-to-end on DEV
(`SO-TEST-GH-0721A`, `UNAME=SAP_CLOUDAI`).

**2026-07-24** — QA verified end-to-end, both routes
(`SO-QA-UT-0724A` / `SO-QA-GH-0724A`). QA RFC user is `BRONZE_BOT`.

**2026-07-27** — PROD confirmed live and carrying real UT traffic
(`VSH-1-ODR-127` / `128`, `ST_CD=HD24`, `UNAME=POWERBI`). Array-shape support
shipped (`6573857`, `27530bf`) and verified on QA: both shapes on both routes,
`200`-with-`Status:"E"` semantics confirmed, true minimum payload established,
retry safety confirmed by item-table read-back.
