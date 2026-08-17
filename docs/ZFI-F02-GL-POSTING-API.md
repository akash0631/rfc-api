# F-02 G/L Posting API (`Z_FI_F02_POST`) — PROD

> **Status:** LIVE on **dev, qa and prod** · **RFC:** `Z_FI_F02_POST` · **FG:** `ZFI_F02P1`
> **Surface:** the generic proxy — `POST /api/rfc/proxy?env=prod`. **No dedicated controller, no deploy.**
> **PROD verified:** 2026-08-17 — four test-run variants passed `BAPI_ACC_DOCUMENT_CHECK` on `S4PCLNT600`.
> **Transport:** `S4DK928028` — **already in PROD** (`TFDIR.FMODE='R'`, all 17 params `R3STATE='A'`).

Posts FI accounting documents — the headless equivalent of t-code **F-02** — from the website.
The caller sends **posting keys exactly as typed in F-02** (`21`, `31`, `40`, `50`); the FM reads
`TBSL` per key to get `KOART` + `SHKZG`, routes each line to `ACCOUNTPAYABLE` (vendor) or
`ACCOUNTGL`, and derives the debit/credit sign. Then `BAPI_ACC_DOCUMENT_CHECK` →
`BAPI_ACC_DOCUMENT_POST` → `BAPI_TRANSACTION_COMMIT`.

---

## ⚠️ Read this before integrating

### 1. PROD demands three fields that DEV and QA do not

This is the single biggest trap. A payload that posts cleanly on DEV/QA is **rejected in PROD**.
Discovered on the first PROD test run, 2026-08-17:

| Field | Where | Why PROD rejects without it |
|---|---|---|
| **`GSBER`** (business area) | **every line** | New G/L document splitting has Business Area as a mandatory balancing characteristic → `GLT2 201 Balancing field "Business Area" in line item 001 not filled` |
| **`BUPLA`** (business place) | **vendor lines only** (`KOART='K'`) | Custom V2 validation → `ZFI01 000 Enter the Business Place for Vendor Tax Transaction`. Fires on `ACCOUNTPAYABLE`. |
| **`PRCTR`** (profit centre) | **G/L lines only** (`KOART='S'`) | Document splitting balancing characteristic → `GLT2 201 ... "Profit Center" ...`. The splitter derives it onto the vendor line — do **not** send it on the vendor line. |

`GSBER` is the new one. `PRCTR` was already known from QA; `BUPLA` was believed to be
*derived* from the vendor line — in PROD the custom check demands it **explicitly**.

### 2. `HTTP 200` does NOT mean the document was posted

The proxy returns `200` whenever the RFC call completes. Business rejections come back as
`200` with `E_SUCCESS: "E"`.

**Branch on `E_SUCCESS == "S"`, never on the HTTP status code.** On `"E"` nothing was written —
the FM calls `BAPI_TRANSACTION_ROLLBACK` itself.

### 3. `?env=prod` posts real money into company code 1100

Global rule: `?env=prod` is **triage only, never a default**. Every integration test must send
`I_TESTRUN: "X"` first. A live PROD post needs Akash's sign-off, and is reversible only by an
FB08 reversal.

---

## Endpoint

```
POST https://sap-api.v2retail.net/api/rfc/proxy?env=prod
Header: X-RFC-Key: v2-rfc-proxy-2026
Content-Type: application/json
```

| `?env=` | SAP target | Use |
|---|---|---|
| *(omitted)* / `dev` | `.174` / 210 | default — integration work |
| `qa` | `.179` / 600 | UAT sign-off |
| `prod` | `.170` / 600 (`S4PCLNT600`) | go-live only |

The response echoes `_ENV`, so you can always confirm which system answered.

> **Why the proxy and not `/api/execute/{code}`?** The dynamic registry cannot carry this RFC.
> `RfcExecuteController.ApplyParams` binds only `Type == "Scalar"` params via `SetValue`, so
> `IT_ITEM` line items would never be filled, and `ExtractRows` returns exactly one table, so
> `E_BELNR` / `E_GJAHR` / `E_SUCCESS` would be dropped. A registry row would give you an
> endpoint that posts nothing and returns no document number. Use the proxy.

---

## Request

### Header parameters — all 9 are MANDATORY

Send every key. Use a blank string where a value does not apply — **never omit a key**.

| Key | Type | Example | Notes |
|---|---|---|---|
| `bapiname` | — | `Z_FI_F02_POST` | proxy routing key, not an FM param |
| `I_BUKRS` | BUKRS | `1100` | V2 Retail Ltd. Validated against `T001`. |
| `I_BLART` | BLART | `SA` | document type |
| `I_BLDAT` | BLDAT | `20260817` | document date, `yyyyMMdd` |
| `I_BUDAT` | BUDAT | `20260817` | posting date, `yyyyMMdd` |
| `I_MONAT` | MONAT | `00` | `00` lets SAP derive the period — recommended |
| `I_WAERS` | WAERS | `INR` | blank defaults to `INR` in the FM |
| `I_XBLNR` | XBLNR | `WEB-1001` | reference doc no. **No idempotency check — see open items.** |
| `I_BKTXT` | BKTXT | `Website F-02` | header text |
| `I_TESTRUN` | CHAR1 | `X` | `X` = check only, no document. Anything else posts. |

### `IT_ITEM` — line items (JSON array, structure `ZFI_F02_ITEM`)

Minimum **2 lines**, and debits must equal credits or the FM rejects before touching SAP.

| Field | DDIC | Required | Notes |
|---|---|---|---|
| `ITEMNO` | NUMC 10 | recommended | caller value wins; blank → sequence. Echoed zero-padded (`0000000001`). |
| `BSCHL` | CHAR 2 | **yes** | posting key as typed in F-02 |
| `ACCOUNT` | CHAR 10 | **yes** | vendor or G/L. ALPHA-converted server-side, so `V00822` and `200001` both work. |
| `WRBTR` | DEC 13.2 | **yes** | **always positive** — direction comes only from `BSCHL`. Must be `> 0`. |
| `GSBER` | CHAR 4 | **yes in PROD** | business area, every line. `1000` is valid in PROD. |
| `BUPLA` | CHAR 4 | **vendor lines** | business place, e.g. `HR01`. `J_1BBRANCH` for CC 1100. Ignored on G/L lines — `BAPIACGL09` has no such field. |
| `SECCO` | CHAR 4 | optional | section code, vendor lines only |
| `ZFBDT` | DATS 8 | optional | baseline date, vendor lines only |
| `KOSTL` | CHAR 10 | optional | cost centre, G/L lines only. See the `KI 281` note below. |
| `PRCTR` | CHAR 10 | **G/L lines in PROD** | profit centre, e.g. `RH01` |
| `ZUONR` | CHAR 18 | optional | assignment. **Auto-filled from the account sort key even when sent blank.** |
| `SGTXT` | CHAR 50 | optional | line item text |

### Supported posting keys

Only account types **K** (vendor) and **S** (G/L) are accepted — a customer key such as `01`
is rejected with `account type D is not supported`.

| Key | Type | Dr/Cr | Proven |
|---|---|---|---|
| `21` | vendor | debit | ✅ DEV, QA (doc `3100377673`), PROD test run |
| `31` | vendor | credit | ✅ DEV, QA (doc `3100377674`), PROD test run |
| `40` | G/L | debit | ✅ DEV, QA, PROD test run |
| `50` | G/L | credit | ✅ DEV, QA, PROD test run |
| `25` | vendor | debit | ⚠️ never tested |

---

## Response

All EXPORTING and TABLES params come back as top-level keys.

| Key | Meaning |
|---|---|
| `E_SUCCESS` | **`S`** = posted (or test run OK) · **`E`** = nothing written. **Branch on this.** |
| `E_MESSAGE` | human-readable summary |
| `E_BELNR` | document number — blank on test runs and failures |
| `E_GJAHR` / `E_BUKRS` | fiscal year / company code |
| `E_OBJ_KEY` | 20-char `BELNR + BUKRS + GJAHR` |
| `ET_RETURN` | full `BAPIRET2` log. `TYPE='W'` entries do **not** block posting. |
| `_ENV` | `dev` \| `qa` \| `prod` — which system answered |
| `_PARAMS_APPLIED` | every param the proxy actually bound. **Check this if a field seems ignored.** |
| `_PARAM_ERRORS` | present only when the proxy rejected a key (typo in a field name) |

Success looks like this:

```json
{
  "E_SUCCESS": "S",
  "E_MESSAGE": "Test run OK - document not posted",
  "E_BELNR": "",
  "ET_RETURN": [
    { "TYPE": "S", "ID": "RW", "NUMBER": "614",
      "MESSAGE": "Document check - no errors: BKPFF $ S4PCLNT600" }
  ],
  "_ENV": "prod"
}
```

---

## PROD verification, 2026-08-17

All four via `?env=prod` with `I_TESTRUN='X'` — `BAPI_ACC_DOCUMENT_CHECK` only, **no COMMIT,
no documents created**. Company code `1100`, doc type `SA`, `GSBER='1000'`, `PRCTR='RH01'`.

| # | Payload | Result |
|---|---|---|
| 0 | `21` V00822 + `50` 2412000039, **no `GSBER`, no `BUPLA`** | ❌ `E` — `GLT2 201` business area + `ZFI01 000` business place. **This is the DEV/QA-shaped payload.** |
| A | `21` V00822 + `50` 2412000039, `GSBER` + `BUPLA=HR01` | ✅ `S` — `RW 614 no errors` |
| B | pure G/L `40`/`50`, **no `BUPLA`** | ✅ `S` — confirms `BUPLA` is only needed when a vendor line is present |
| C | `31` V35113 + `40` 2412000039 (return direction) | ✅ `S` — the vendor from the F-02 screenshots |
| D | as A **plus `KOSTL='RH0100'`** | ✅ `S` **with warning** `KI 281 Do not assign any objects in cost accounting to account 2412000039` |

PROD master data confirmed by direct table reads:

- Vendors `V35113` (SARVPRIYA) and `V00822` — both exist, `SPERR`/`LOEVM` blank.
- G/L `2412000039` in CC `1100` — `XOPVW='X'` (open item managed), sort key `ZUAWA='001'`
  (this is why `ZUONR` auto-fills), field status group `G001`.
- Profit centre `RH01` — valid to `99991231`, not locked, `KOKRS=1000`.
- Cost centre `RH0100` — exists, assigned to profit centre `RH01`.
- **`2412000039` is NOT a cost element in PROD** — `CSKB` is empty for `KOKRS='1000'`.
  This closes an open question from the build note: DEV, QA **and PROD** all agree.
  Sending `KOSTL` for this account is tolerated as warning `KI 281` and the cost centre is
  dropped, so leave `KOSTL` blank for non-cost-element accounts to keep the log clean.

---

## Copy-paste

### PROD test run (safe — creates nothing)

```bash
curl -X POST 'https://sap-api.v2retail.net/api/rfc/proxy?env=prod' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -H 'Content-Type: application/json' \
  -d '{
    "bapiname": "Z_FI_F02_POST",
    "I_BUKRS": "1100", "I_BLART": "SA",
    "I_BLDAT": "20260817", "I_BUDAT": "20260817",
    "I_MONAT": "00", "I_WAERS": "INR",
    "I_XBLNR": "WEB-1001", "I_BKTXT": "Website F-02",
    "I_TESTRUN": "X",
    "IT_ITEM": [
      {"ITEMNO":"001","BSCHL":"21","ACCOUNT":"V00822","WRBTR":"10000.00",
       "GSBER":"1000","BUPLA":"HR01"},
      {"ITEMNO":"002","BSCHL":"50","ACCOUNT":"2412000039","WRBTR":"10000.00",
       "GSBER":"1000","PRCTR":"RH01"}
    ]
  }'
```

To post for real, change `"I_TESTRUN": "X"` to `"I_TESTRUN": ""`. **Requires sign-off.**

### PowerShell

A ready-to-run script lives outside the repo at
`C:\Users\Administrator\claude\scripts\zfi_f02\prod_dryrun.ps1` (test run, PROD).

---

## Gotchas

1. **`BAPIACGL09` has no `BUSINESSPLACE` / `SECTIONCODE`.** Business place cannot be pushed onto
   a G/L line at all. In a mixed document SAP derives it from the vendor line; in a pure G/L
   document `BUPLA` stays blank on every line. Verified in DEV, QA and PROD.
2. **All 9 IMPORTING params are mandatory** — send blanks, never omit a key.
3. **Amounts are always positive.** Sending a negative `WRBTR` fails validation V6
   (`amount must be greater than zero`), it does not flip the sign.
4. **The FM validates before it touches SAP:** company code exists (`T001`), ≥ 2 lines, posting
   key defined (`TBSL`), account type K/S only, account filled, amount > 0, debits = credits.
   These come back as `ID='ZF02'` messages.
5. **`I_MONAT='00'`** correctly lets SAP derive the period. Verified: `05` derived for 11.08.2026.
6. **Warnings do not block.** The FM only treats `TYPE='E'` and `TYPE='A'` as failures, so a
   document with `KI 281` still posts.

---

## Open items before go-live

- [ ] **No idempotency.** Nothing stops the same `I_XBLNR` posting twice — two clicks on the
      website = two FI documents. Needs a duplicate check (`BKPF-XBLNR` lookup) either in the
      FM or in the caller.
- [ ] **Auth is a shared key.** `X-RFC-Key: v2-rfc-proxy-2026` is a hardcoded constant in a
      **public** repo, and it authorises *any* RFC in PROD, not just this one. Acceptable for
      triage; not acceptable as the website's production posting path. If this endpoint goes
      live for the website, it should get a dedicated controller with its own credential and
      payload validation.
- [ ] **Live PROD post never executed.** Test runs only. First real document needs Akash's
      sign-off; verify with FB03 and be ready to reverse via FB08.
- [ ] Doc types beyond `SA` unconfirmed. Customer lines (`KOART='D'`) unsupported by design.
- [ ] Posting key `25` still untested.

## Related

- `docs/RFC-API-OVERVIEW.md` — all endpoints
- `Controllers/Generic/GenericRfcProxyController.cs` — the surface this API rides on
- `Controllers/RfcSync/RfcExecuteController.cs` — the registry path, and why it can't be used here
- Vault note: `v2retail/RFC - F-02 GL Posting Z_FI_F02_POST.md`
