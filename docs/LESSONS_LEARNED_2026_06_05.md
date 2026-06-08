# Lessons Learned — ZART_CHAR_PATCH V61 Ship (2026-06-05)

Distilled rules for V2 SAP/RFC ship work. Apply on every future FM ship. Authored after a 4-hour session that should have been 10 minutes.

## Rule 1: Smoke FIRST, debug LATER

When you see a red entry or RC > 4 in `STMS_MONI`, **do not** start decoding the tp log tree, building fix-up TRs, or calling Akash for manual STMS clicks.

Instead, smoke the functional path immediately:

```bash
curl -X POST '<api>/<endpoint>?env=qa' -d '{...}'
```

```
sap_read_table TFDIR env=qa where="FUNCNAME='Z_X'"
```

If smoke returns `ok:true` and TFDIR row exists, **the FM is LIVE**. The tp log RC is cosmetic noise from dead objects in the same TR. Tell the user "live, mission done". **Stop investigating.**

This rule alone would have saved ~2 hours on 2026-06-05.

## Rule 2: One ship = one keeper FG = one TR

Yesterday's 10-min ships looked like:

```
CREATE_TR → sap_build_rfc (one FG, one FM) → DEV smoke → release → STMS QA → QA smoke
```

This session's 4-hour ship looked like:

```
CREATE_TR
→ sap_build_rfc V6FG1 (probe — wrong route)
→ sap_build_rfc V6FG2 (probe — wrong BAPI param)
→ sap_build_rfc V6FG3 (probe — wrong KSML scan)
→ sap_build_rfc V6SMK1..V6SMK6 (EXEC_PROG smoke wrappers, blocked by STRING-unsupported)
→ sap_build_rfc V6PR1..V6PR4 (BAPI parameter probes)
→ sap_build_rfc V61FG1 (FINALLY working)
→ release → STMS QA → RC 16 (14 dead FGs failed activation) → user perceived as broken
```

**Before TR_RELEASE, purge all probe/smoke/abandoned FGs from the TR** via `Z_CLAUDE_TR_OBJ_PURGE` (FG `ZCLAUDE_TROBJP1`, TR `S4DK925593`). Use `sap_tr_manifest` to identify what to drop. Ship only the keeper.

## Rule 3: Verify dispatcher wrappers always

`sap_dispatcher action=RELEASE_TR` returns `ok:true` and `E070.TRSTATUS='R'`. But the tp forwarding step (cofile write + TMSBUFFER outbound population) does NOT always fire from this action. Other TRs released around the same time may forward correctly while yours doesn't.

Verify after every wrapper call:

```
sap_read_table TMSBUFFER env=dev where="TRKORR='<TR>' AND SYSNAM='S4Q'"
# Expect: 1 row with IMPFLG in (k,t,w). If 0 rows after 5 min, SE10 manual release.
```

This is part of the larger `Wrapper subrc=0 lies` family of bugs.

## Rule 4: Don't reuse FM names mid-debug

`DELETE_FM Z_X` + `CREATE_FM Z_X` in a new FG can leave the RFC kernel cache resolving to the stale binary. `abap_read_source` shows the new code, `TFDIR` shows the new FG, but smoke still hits old logic.

**Always bump the FM suffix** (`V6` → `V61`) when redeploying during active debug.

## Rule 5: Strip `FUNCTION`/`ENDFUNCTION` wrapper from `sap_build_rfc body`

`sap_build_rfc` adds its own `FUNCTION` header from `imports`/`exports`/`tables`. Passing a body that includes outer `FUNCTION Z_X.` ... `ENDFUNCTION.` produces double-wrap and compile failure.

When porting source from `READ_PROG LZ<FG>U01`, strip lines 1 (`FUNCTION ...`), the interface header `*"` block, and the trailing `ENDFUNCTION.`. Keep only TYPES/DATA/body.

## Rule 6: `Get-Content -Raw` returns PSObject, not String

```powershell
$source = Get-Content -Raw path/to/source.abap    # ❌ PSObject with PSPath/PSParentPath metadata
$source = [IO.File]::ReadAllText("path/to/source.abap")    # ✅ plain string
```

`ConvertTo-Json` serializes the PSObject's metadata properties, producing garbage in JSON payloads — including ABAP source uploads to `sap_dispatcher action=CREATE_PROG`. The compiled program will be unusable.

## Rule 7: Cofile propagation lag

After `RELEASE_TR` on DEV, the cofile (`/usr/sap/trans/cofiles/K<num>.S4D`) takes 1-5 minutes to propagate to QA host's filesystem. Until then, STMS Add → Other Requests → Add returns empty `REQTXT` validation.

If still empty after 10 minutes, the forwarding daemon may be stalled (it was frozen for 2 days in 2026-06-05 session, last forwarded TR was S4DK925628 on 2026-06-03). Workaround: manual SE10 release on DEV side or wait for daemon batch.

## Rule 8: STMS Add → Other Requests → Add bypasses forwarding lag

When TMSBUFFER QA shows 0 rows but cofile exists on filesystem, force-add via SAP GUI:

```
/nSTMS_IMPORT on S4Q → Extras → Other Requests → Add → enter TRKORR → Save
```

This pulls the TR from cofile directly into S4Q's import queue without waiting for the forwarding daemon. Returns "already in queue" info popup if TR is already buffered.

VBS automation: `~/claude/scripts/v61_qa_add_v2.vbs`. Coord pattern for STMS_IMPORT ALV: `wnd[0]/usr/sub/1[0,0]/sub/1/5[0,Y]/lbl[X,N]` — Y/N shift per render, scan loop required.

## Rule 9: Single self-hosted runner = serialization tax

`rfc-api` repo deploys IIS via single self-hosted runner `V2DC-ADDVERB`. Long-running workflows (BRONZE Snowflake sync, 50+ table loads, 2-3 hour runtime) block IIS deploys for 30 min — 2 hr. Plan ship time accordingly.

To unblock: either wait, cancel the blocking workflow (destructive — mid-load data sync), or add a second runner (requires Akash action).

## Rule 10: Always pass `dev_tr` on `sap_build_rfc`

Hardest rule in the SAP development kit. Per global instructions:

```
ONE development = ONE TR.
STEP 1: sap_dispatcher action=CREATE_TR args={IM_TR_TEXT:'<FM>: <purpose>'}
STEP 2: pass returned TR as dev_tr to EVERY sap_build_rfc, sap_rpy_create_fm, sap_dispatcher call
STEP 3: sap_tr_manifest at end to verify
```

Without `dev_tr`, objects scatter across system-default open tasks or become orphans with zero E071 entries, requiring `Z_CLAUDE_TR_REG` rescue.

## Anti-patterns checklist (paste in PR description for SAP ships)

- [ ] FM source stripped of FUNCTION/ENDFUNCTION wrapper before `sap_build_rfc`
- [ ] FM source read via `[IO.File]::ReadAllText`, not `Get-Content -Raw`
- [ ] All `sap_build_rfc` / `sap_dispatcher` calls passed `dev_tr=<TR>`
- [ ] `sap_tr_manifest` confirms only keeper FG in TR (no probe/smoke pollution)
- [ ] TMSBUFFER DEV verified after RELEASE_TR (1 row SYSNAM=S4Q, IMPFLG=k/t/w)
- [ ] DEV smoke via curl returns `ok:true` before any QA ship attempt
- [ ] QA smoke verified after STMS import (TFDIR row + curl smoke)
- [ ] STMS_MONI RC > 4 investigated only if smoke also fails (RC alone is not enough)

## Reference: 10-min canonical ship (yesterday's flow)

```bash
# 1. Plan: 5 min
#    Read existing FM, identify change, write new body. No probe FGs.

# 2. Build: 2 min
sap_dispatcher CREATE_TR args={IM_TR_TEXT:"Z_X: <purpose>"}
# returns S4DK<TR>

sap_build_rfc fm_name=Z_X fg_name=ZX_CLEAN dev_tr=<TR> body=<source> ...

# 3. DEV smoke: 1 min
curl -X POST '<api>?env=dev' -d '<payload>'
# ok:true → proceed

# 4. Release: 1 min
sap_dispatcher RELEASE_TR IM_TR_NUMBER=<task TR>
sap_dispatcher RELEASE_TR IM_TR_NUMBER=<parent TR>
sap_read_table E070 where="TRKORR='<TR>'"  # confirm both TRSTATUS=R

# 5. STMS QA: 1 min
# Wait 1-5 min for cofile, then SAP GUI STMS_IMPORT → find row → Import → Ignore Component Version → Yes

# 6. QA smoke: 30 sec
curl -X POST '<api>?env=qa' -d '<payload>'
# ok:true → ship done
```

Total: ~10 minutes when no probe FG pollution, no forwarding lag, no runner queue.

When everything blocks at once (as on 2026-06-05): ~4 hours. Drives Rule 1 home.
