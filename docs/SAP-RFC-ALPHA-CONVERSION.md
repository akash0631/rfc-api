# SAP RFC-via-REST always needs explicit ALPHA / MATN1 conversion

> **TL;DR:** When an SAP FM is wrapped behind `sap-api.v2retail.net` (the .NET REST gateway), input keys arrive in **external format** (`"100000"`, `"1"`) but DDIC tables store them in **internal format** (`"0000100000"`, `"000000000000000001"`). Any `SELECT … WHERE field = @input` silently returns zero rows unless the FM converts inputs first.

## The incident — 2026-05-08

`ZMM_PO_CREATION_RFC` was promoted DEV → QA. The API at `https://sap-api.v2retail.net/api/ZMM_PO_CREATION_RFC` started failing with `500 {"Status":false,"Message":"no SAP ErrInfo available"}`.

User-reported symptom (verbatim):
> "data is going in internal table btt nrkspace ... thats why the firs sleect query is not workign"

Translation: *"Data is going into the internal table but not into the workspace, so the first SELECT query is not working."* That was a **literal and correct** description of the bug.

## Root cause

The first SELECT in the FM was:

```abap
SELECT E~MATNR, E~INFNR, E~LIFNR, EI~MWSKZ
  FROM EINA AS E
  INNER JOIN EINE AS EI ON E~INFNR = EI~INFNR
  FOR ALL ENTRIES IN @LT_ITEMS
  WHERE E~MATNR = @LT_ITEMS-MATERIAL
    AND E~LIFNR = @IV_VENDOR
  INTO TABLE @LT_EINE.
```

REST caller sends JSON like:

```json
{ "IV_VENDOR": "100000", "IT_ITEMS": [{ "MATERIAL": "1", … }] }
```

The .NET wrapper passes those raw strings straight through to SAP. EINA / EINE / MARA / LFA1 / KNA1 / EKKO all store keys in **internal SAP format** — left-zero-padded for vendor/customer/PO numbers, MATN1-converted for materials. The comparison happens like:

```
EINA-LIFNR = '0000100000'  vs  @IV_VENDOR = '100000'        → no match
EINA-MATNR = '000000000000000001'  vs  '1'                  → no match
```

Result: SELECT returns 0 rows → `LT_EINE` empty → `EXPORT … TO MEMORY ID 'Z_PO_TAX'` skipped → downstream `ZMM_POCREATE` runs without tax codes → BAPI short-dumps with no usable `BAPIRET2`, hence the gateway's *"no SAP ErrInfo available"*.

## The fix (Aashna, 2026-05-08)

Five lines added in the FM, immediately before the SELECT:

```abap
LOOP AT LT_ITEMS ASSIGNING FIELD-SYMBOL(<FS_ITEMS>).
  CALL FUNCTION 'CONVERSION_EXIT_MATN1_INPUT'
    EXPORTING  INPUT  = <FS_ITEMS>-MATERIAL
    IMPORTING  OUTPUT = <FS_ITEMS>-MATERIAL.
ENDLOOP.

IV_VENDOR = |{ IV_VENDOR WIDTH = 10 ALIGN = RIGHT PAD = '0' }|.
```

No structural change, no DDIC change, no transport of `ZMM_POCREATE`. FM normalizes keys to internal format before the SELECT runs.

## Why this is reusable knowledge

The .NET RFC wrapper at `sap-api.v2retail.net` does **not** apply ALPHA conversion. SAP GUI does. NW Gateway sometimes does. The custom REST gateway does not. **Every Z-FM exposed through it has to handle this itself.**

## Conversion cheat-sheet for V2 RFCs

| Field type        | Data element examples       | Internal-format conversion                                                          |
|-------------------|-----------------------------|-------------------------------------------------------------------------------------|
| Material number   | `MATNR`, `MATNR18`          | `CALL FUNCTION 'CONVERSION_EXIT_MATN1_INPUT'`                                       |
| Vendor            | `LIFNR`, `ELIFN`            | ALPHA: pad to 10 with `'0'` — inline `\|{ x WIDTH = 10 ALIGN = RIGHT PAD = '0' }\|`  |
| Customer          | `KUNNR`                     | ALPHA: pad to 10                                                                    |
| Company code      | `BUKRS`                     | ALPHA: pad to 4                                                                     |
| Plant             | `WERKS`, `EWERK`            | Usually pass-through (4-char alpha) — verify against DDIC                           |
| Storage location  | `LGORT_D`                   | ALPHA: pad to 4                                                                     |
| PO number         | `EBELN`                     | ALPHA: pad to 10                                                                    |
| Cost center       | `KOSTL`                     | ALPHA: pad to 10                                                                    |
| GL account        | `SAKNR`                     | ALPHA: pad to 10                                                                    |
| Article (retail)  | `MATNR` retail length       | MATN1 — same as material                                                            |

One-shot left-zero-padding, inline string template (cheaper than function call):

```abap
" ALPHA equivalent for any 10-char left-zero-padded field
lv_padded = |{ lv_input WIDTH = 10 ALIGN = RIGHT PAD = '0' }|.
```

For materials, must use the function — conversion isn't a simple zero-pad:

```abap
CALL FUNCTION 'CONVERSION_EXIT_MATN1_INPUT'
  EXPORTING  INPUT  = lv_external_matnr
  IMPORTING  OUTPUT = lv_internal_matnr.
```

## Symptom signature → diagnostic ladder

When this bug ships:

1. **Empty result on a SELECT that should return data.** First check.
2. **API gateway returns `500 {"Message":"no SAP ErrInfo available"}`** → SAP-side dump with no `BAPIRET2`. Check ST22 in the env you're hitting.
3. **FM "works for some inputs, not others"** — callers sometimes happen to send already-padded values and sometimes don't.
4. **Fields you expect populated downstream are blank** (tax codes, addresses) — empty-SELECT cascading silently.

Diagnostic order:

1. Hit the API with a known-good payload.
2. If 500 / empty → read the FM source. Look for any `WHERE E~field = @input` against `MARA`, `LFA1`, `KNA1`, `EINA`, `EINE`, `EKKO`, `EKPO`, `T001`.
3. Check whether the input is converted to internal format before that SELECT.
4. If not, add the conversion.

## Pre-deploy checklist for any new V2 Z-FM behind sap-api.v2retail.net

- [ ] Identify all input keys (IMPORTING + TABLES line types) that map to data elements in the table above
- [ ] For each, add the conversion call/inline pad **before** the first SELECT that uses it
- [ ] Test with intentionally **un-padded** input (e.g., vendor `"1"` not `"0000000001"`) and confirm the SELECT still returns rows
- [ ] Lint: any `WHERE field = @input` against MARA / EINA / LFA1 / KNA1 / EKKO without a preceding conversion is a smell

## Related

- [PO-CREATOR-API.md](./PO-CREATOR-API.md) — upstream consumer of `ZMM_PO_CREATION_RFC`
- [RFC-API-OVERVIEW.md](./RFC-API-OVERVIEW.md) — gateway architecture
