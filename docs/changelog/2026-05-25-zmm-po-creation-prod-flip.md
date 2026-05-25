# 2026-05-25 — `ZMM_PO_CREATION_RFC` API flipped QA → PROD

## What changed

`master` commit `561c3f8`. One file:

`Controllers/MM/ZMM_PO_CREATION_RFCController.cs`
- `BaseController.rfcConfigparametersquality()` → `rfcConfigparametersproduction()`
- Header comment: `SAP Target: QUALITY (.179 / Client 600 / S4Q)` → `PRODUCTION (.170 / Client 600 / PRD)`

CI workflow `Build and Deploy to .36 IIS` — run `26393122103` — completed in 2m7s. Endpoint `https://sap-api.v2retail.net/api/ZMM_PO_CREATION_RFC` now hits S/4 PROD.

## Why one-line was enough

The SAP-side FM (`ZMM_PO_CREATION_RFC` in FG `ZPO_CREATION_FG`) was already live on PROD via earlier transport — only the .NET REST controller still pointed at QA. `BaseController` already had `rfcConfigparametersproduction()` returning `.170 / Client 600 / POWERBI / PRD`. Swap method name → routing flips.

## Verification fingerprint

PROD ELSE branch in FM uses lowercase: `| Please provide all mandatory inputs |`.
DEV/QA ELSE branch uses uppercase: `| PLEASE PROVIDE ALL MANDATORY INPUTS |`.

Post-deploy smoke returned the lowercase variant → confirmed PROD routing. Re-use this casing diff as a low-risk env check for `ZMM_PO_CREATION_RFC` without firing a real PO.

```bash
curl -s -X POST "https://sap-api.v2retail.net/api/ZMM_PO_CREATION_RFC" \
  -H "Content-Type: application/json" \
  -d '{"IV_VENDOR":"100000","IT_ITEMS":[{"MATERIAL":"1","QTY":"1","NET_PRICE":"1","PLANT":"INVALID","STORAGE_LOC":"0001","DEL_DATE":"20260601"}]}'
# PROD: {"Status":false,"Message":" Please provide all mandatory inputs","PoNumber":""}
# QA:   {"Status":false,"Message":" PLEASE PROVIDE ALL MANDATORY INPUTS","PoNumber":""}
```

## What was NOT changed

- SAP FM source — untouched in both envs
- ALPHA / MATN1 conversion fix from 2026-05-08 — already in PROD source
- `BaseController` env methods — untouched
- All other controllers — untouched

## Follow-ups for app team

PO Creator app (Lovable `po-wise-wardrobe`) should re-test the same URL with a real payload (vendor, material, plant, qty, price, storage_loc, del_date). Field name in `IT_ITEMS` is `QTY` (not `QUANTITY`) per `ZMM_PO_CREATION_ItemRow` in the C# controller — surface this to UI devs if smoke shows `IT_ITEMS IS INITIAL`.

End-to-end smoke (real PO create in PRD client 600) is pending — needs valid LIFNR + MATNR + EWERK from app team.

## Refs

- Controller: [`Controllers/MM/ZMM_PO_CREATION_RFCController.cs`](../../Controllers/MM/ZMM_PO_CREATION_RFCController.cs)
- Overview: [../RFC-API-OVERVIEW.md](../RFC-API-OVERVIEW.md)
- API contract: [../PO-CREATOR-API.md](../PO-CREATOR-API.md)
- Original ALPHA fix lesson: [../SAP-RFC-ALPHA-CONVERSION.md](../SAP-RFC-ALPHA-CONVERSION.md)
- CI run: https://github.com/akash0631/rfc-api/actions/runs/26393122103
