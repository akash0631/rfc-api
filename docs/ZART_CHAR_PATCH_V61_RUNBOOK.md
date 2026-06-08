# ZART_CHAR_PATCH V61 Build + Ship Runbook

Multi-class routing fix for `Z_ART_PATCH_RFC` FM that resolves V4 `NOT_IN_CLASS` error on 18 KLART 026 article-creation fields (`M_FAB_MAIN_MVGR_1`, etc.).

**Ship date**: 2026-06-05
**Status**: V61 LIVE DEV + QA. PROD pending.

## Problem statement

V4 (`Z_ART_PATCH_RFC_V4`) only scanned material classification via `KSSK(OBJEK=MATNR, KLART='001')` (general material class).

The 18 new article-creation fields live in KLART `026` (variant configuration class), reached via:

```
INOB(MATNR, OBTAB=MARA) → CUOBJ → KSSK(CUOBJ, KLART='026') → CLINT
```

Caller PATCH on `M_FAB_MAIN_MVGR_1` returned:

```json
{"matnr":"1124025475","ok":false,"results":[{"fn":"M_FAB_MAIN_MVGR_1","route":"NONE","status":"NOT_IN_CLASS"}]}
```

## V61 fix

1. Enumerate ALL candidate classes for MATNR:
   - KLART `001` direct (V4 path retained)
   - INOB chain for any KLART
2. Per-attr scan all candidate KSMLs, pick first hosting class
3. Group BAPI calls by `(CLASSTYPE, CLASS)`, one BAPI call per group
4. **CRITICAL**: `BAPI_OBJCL_CHANGE` always invoked with `OBJECTKEY = MATNR` even for KLART 026 — BAPI resolves CUOBJ internally via INOB. Passing CUOBJ directly returns `CL/763 Object does not exist`.

## SAP artifacts

| Component | Name                          | TR             |
|-----------|-------------------------------|----------------|
| FM        | `Z_ART_PATCH_RFC_V61`         | `S4DK925666`   |
| FG        | `ZARTPV61FG1`                 | (child task `S4DK925667`) |
| PNAME     | `SAPLZARTPV61FG1`             |                |
| Include   | `LZARTPV61FG1U01` (417 lines) |                |

## C# controller flip

Repo: `akash0631/rfc-api`
File: `Controllers/MM/ArticleCharController.cs`
Change: line ~201, swap `Z_ART_PATCH_RFC_V4` → `Z_ART_PATCH_RFC_V61`

Merged via PR #24 → `staging` branch → CI auto-merge to `master` → IIS .36 redeploy.

## Verification (DEV + QA)

```bash
# DEV test mode
curl -X POST 'https://sap-api.v2retail.net/api/article/patch?env=dev' \
  -H 'Content-Type: application/json' \
  -d '{"matnr":"1124000080","changes":{"M_FAB_MAIN_MVGR_1":"SLD"},"testMode":true,"user":"VERIFY"}'

# Expected: ok:true, plan:[{fn:M_FAB_MAIN_MVGR_1, route:AUSP, klart:026, class:120401001}]

# QA test mode
curl -X POST 'https://sap-api.v2retail.net/api/article/patch?env=qa' \
  -H 'Content-Type: application/json' \
  -d '{"matnr":"1124025475","changes":{"M_FAB_MAIN_MVGR_1":"SLD"},"testMode":true,"user":"VERIFY"}'

# Expected: ok:true, plan:[{fn:M_FAB_MAIN_MVGR_1, route:AUSP, klart:026, class:123020607}]

# QA real write
curl -X POST 'https://sap-api.v2retail.net/api/article/patch?env=qa' \
  -H 'Content-Type: application/json' \
  -d '{"matnr":"1124025475","changes":{"M_FAB_MAIN_MVGR_1":"SLD"},"testMode":false,"user":"VERIFY"}'

# Expected: ok:true, applied:1, results:[{fn:..., route:AUSP, klart:026, class:123020607, status:APPLIED}]
```

AUSP write verified end-to-end: MATNR `000000000008114524` ATINN `0000001257` ATWRT `CHK` → `SLD` (and back).

## Ship process (canonical 10-min flow)

This is what should have been done first time. The actual ship hit cosmetic noise — see `LESSONS_LEARNED_2026_06_05.md`.

```
1. Read V4 source from include  →  read_prog LZARTPV4U01
2. CREATE_TR with descriptive text:
   sap_dispatcher action=CREATE_TR args={IM_TR_TEXT:'<FM_NAME>: <purpose>'}
3. Build FM in clean named FG (1 keeper, no probes):
   sap_build_rfc fm_name=Z_X fg_name=ZX_CLEAN dev_tr=<TR> body=<source>
4. DEV smoke via curl on /api/<endpoint>?env=dev
5. Verify TR has ONLY keeper FG:
   sap_tr_manifest tr=<TR>
   # Expect: 1 R3TR FUGR. Reject if 2+ FUGRs.
6. If extra probe FGs present (from iterative debug):
   sap_dispatcher action=Z_CLAUDE_TR_OBJ_PURGE for each probe → re-manifest → verify 1 FG
7. RELEASE_TR child task → parent TR. Verify both E070.TRSTATUS=R.
8. Verify forwarding:
   sap_read_table TMSBUFFER env=dev where TRKORR='<TR>' AND SYSNAM='S4Q'
   # Expect: 1 row with IMPFLG in (k, t, w)
   # If 0 rows after 5 min, dispatcher RELEASE_TR skipped tp forwarding —
   # request SE10 manual release.
9. STMS_IMPORT on S4Q:
   a. (Optional) Extras → Other Requests → Add to bypass forwarding daemon lag
   b. Find row → Request → Import → tick Ignore Component Version → Yes
10. Verify QA: smoke curl + sap_read_table TFDIR where FUNCNAME='Z_X'
```

## Anti-patterns observed (2026-06-05)

| Anti-pattern | Cost | Mitigation |
|--------------|------|------------|
| 14 probe/smoke FGs left in parent TR before release | RC 16 cosmetic noise in QA STMS_MONI; user perceived as broken; triggered fix-up loop | Purge before release per Z_CLAUDE_TR_OBJ_PURGE |
| FM name reuse + DELETE_FM + CREATE_FM cycle during debug | RFC kernel cache returns stale binary; abap_read_source shows new code but smoke hits old | Bump FM name suffix (V6 → V61) when in doubt |
| Trusting dispatcher `RELEASE_TR` ok response | TR can flip E070=R without firing tp forwarding | Verify TMSBUFFER DEV outbound has row before STMS attempt |
| Assuming RC 16 in tp log = broken FM | Triggered unnecessary fix-up TR S4DK925713 build + ship loop | Smoke curl FIRST; if ok:true, FM is live, stop. |
| `Get-Content -Raw` PSObject for source upload | Garbage ABAP source uploaded due to JSON serialization of PSObject metadata | Use `[IO.File]::ReadAllText` |

## Related docs

- API contract: [`ARTICLE-CHAR-PATCH-API.md`](./ARTICLE-CHAR-PATCH-API.md)
- Lessons learned: [`LESSONS_LEARNED_2026_06_05.md`](./LESSONS_LEARNED_2026_06_05.md)
- Earlier V3 handover: [`ZART_CHAR_PATCH-SESSION-HANDOVER.md`](./ZART_CHAR_PATCH-SESSION-HANDOVER.md) (superseded by V61)
- SAP wrapper catalog: [`Z_CLAUDE_WRAPPERS_CATALOG.md`](./Z_CLAUDE_WRAPPERS_CATALOG.md)
- TR safety rules: [`SAP-SAFE-PATCH-FM-MCP-SPEC.md`](./SAP-SAFE-PATCH-FM-MCP-SPEC.md)
