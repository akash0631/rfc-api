# SAP FM Patch Playbook — 5-Minute Path

> Canonical SOP for patching an existing SAP RFC function module (FM) headless via Claude/MCP. Read this BEFORE touching any FM. Following this strictly = 5 min build. Skipping it = hours of orphan-include carnage (see [[2026-06-02 ZMM_ART postmortem]]).

## TL;DR — the rule

**Never `DELETE_FM` + recreate.** Build a NEW patched FM in a NEW Function Group. Flip the IIS controller to call the new FM. Release new TR to QA. Done.

## Why "delete + recreate" is forbidden

The dispatcher `DELETE_FM` action clears `TFDIR / ENLFDIR / TFTIT / TADIR` for the target FM, but does **NOT** regenerate `FGUXX` (the auto-generated U-include aggregator), nor wipe the orphan U-include source. When you then `sap_rpy_create_fm` the same FM name, SAP allocates a NEW `LZxxxxFGU0N` include and APPENDS a NEW line to FGUXX. The result:

```
FGUXX now references both:
  INCLUDE LZxxxxFGU01.  " <- ORPHAN with old FUNCTION X. block
  INCLUDE LZxxxxFGU0N.  " <- NEW with same FUNCTION X. block
```

→ duplicate `FUNCTION` declaration in the FG main pool → `Syntax error in SAPLxxxx_FG` → **every FM in that FG dies**, including unrelated sibling FMs other people own. STMS internal regen FMs (`RS_FUNCTION_POOL_REGENERATE`, `TMS_*`) are all RFC-blocked on S/4, so headless repair is impossible. Only fix = SAP GUI manual SE80 → FG → Activate.

DELETE+RECREATE is ONLY safe for FGs containing a single FM (e.g. `ZFA_UPLOAD_ROW`). Pre-check FM count before EVERY DELETE_FM.

## Pre-flight (30 sec)

Before any action, always:

1. **Read the FM source** — `abap_read_source env=dev|qa fm_name=<FM>` and confirm the patch target.
2. **Count FMs in the FG** — `RFC_READ_TABLE TFDIR WHERE PNAME='SAPL<FG>'`. If >1, you are FORBIDDEN to DELETE_FM. Period.
3. **Check QA state** — does QA have the same FM source? If yes, your patch needs to go through DEV→QA transport, not direct on QA.

## The 5-step build (5 minutes)

### Step 1: Build patched FM in FRESH isolated FG

```javascript
sap_build_rfc({
  fm_name: "ZMM_ART_CRT_V3",          // new name, suffix old FM with _V2 / _V3
  fg_base: "ZMM_ART_CRT_V3",          // auto-suffix to V31 / V32...
  short_text: "Article creation V3 (Bug 1+2 patched)",
  imports: [{PARAMETER: "IM_DATA", TYP: "ZTT_ART_CRT", PASS_VALUE: "X", DEFAULT: ""}],
  exports: [{PARAMETER: "EX_DATA", TYP: "ZTT_ART_CRT_RET", PASS_VALUE: "X", DEFAULT: ""}],
  body: `<patched ABAP source — see source rules below>`
})
```

`sap_build_rfc` mints a fresh TR, creates the FG, creates the FM, sets program attributes, activates everything, registers into the TR via `Z_CLAUDE_TR_REG`. One call.

### Step 2: Verify

```javascript
sap_verify_fm({fm_name: "ZMM_ART_CRT_V3"})
// expect: ok:true, fupararef_rows:N, active:N, rfc_params:N
```

### Step 3: Smoke

```javascript
abap_test_fm({fm_name: "ZMM_ART_CRT_V3", env: "dev"})
// expect: syntax_error:false
```

Then real-payload via proxy:
```bash
curl -X POST 'sap-api.v2retail.net/api/rfc/proxy?env=dev' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -d '{"bapiname":"ZMM_ART_CRT_V3", ...payload...}'
```

### Step 4: Flip IIS controller (1 line edit)

```csharp
// Controllers/MM/<RFC>Controller.cs line ~89
RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
IRfcFunction myfun = dest.Repository.CreateFunction("ZMM_ART_CRT_V3");
//                                                  ^^^^^^^^^^^^^^^^ swap to new FM name
```

`git push origin master` → GHA `deploy-iis.yml` builds + deploys + recycles `V2RfcTestPool` on `.36` IIS. ~3 min.

### Step 5: Release + import TR DEV→QA

```javascript
// Release child task then parent
sap_dispatcher({action: "RELEASE_TR", args: {IM_TR_NUMBER: "<child task>"}})
sap_dispatcher({action: "RELEASE_TR", args: {IM_TR_NUMBER: "<dev_tr>"}})
```

Then STMS import — **manual SAP GUI 30 sec** (no RFC-callable alternative):
1. `/nSTMS_QUEUES` → double-click `S4Q` row
2. Ctrl+F → paste TR# → Enter → Cancel
3. Cursor on found row → okcode `=IMPS` → confirm popup

OR drive via VBS (see `~/claude/tmp/sap_stms_drive_v2.vbs` — 80% complete, needs popup-handler polish).

Verify QA via API: `curl ... ?env=qa` — expect `MSG_TYP=S` or clean BAPI validation errors.

## ABAP source rules (avoid 90% of "syntax error" rabbit holes)

| Rule | Why |
|---|---|
| **Lines ≤ 72 chars** | `sap_build_rfc` validator hard-rejects. ABAP itself allows up to 255 but stay disciplined. |
| **No FUNCTION/ENDFUNCTION wrapper** | `sap_build_rfc` and `sap_rpy_create_fm` add it. |
| **Use inline `DATA(LT_XYZ)`** for typed-by-call variables | Pre-declaring with the wrong type (e.g. `STANDARD TABLE OF SBDST_MESSAGE`) causes orphan-include syntax error you cannot un-do headless. If type unknown → inline. |
| **No `TRY/CATCH cx_root` unless you've verified the called method actually raises** | Adds complexity for zero benefit when target is a procedural BAPI wrapper. |
| **No `COND #(...)` if a simple `IF/ELSEIF` works** | Cleaner errors, fewer 72-char overflows. |
| **Classic SQL syntax preferred** | `SELECT SINGLE field FROM tab INTO @lv WHERE k = @x.` — split across lines for 72-col compliance. |
| **Field assigns: split with `=` at column ≥ 36** to give RHS room | Compact, readable, under-72. |

## Naming convention

| What | Pattern | Example |
|---|---|---|
| Patched FM | `<ORIG_BASE>_V<N>` | `ZMM_ART_CRT_V3` |
| Fresh FG | `<FM_NAME>` (auto-suffix `_V31` etc.) | `ZMM_ART_CRT_V31` |
| Dev TR description | `<Domain>: <intent> V<N> (<bugs patched>)` | `Article creation V3 (Bug 1+2 patched)` |

## What to NEVER do

- ❌ `sap_dispatcher action=DELETE_FM` on an FM in a multi-FM FG
- ❌ `sap_dispatcher action=DELETE_FG` on non-`ZCLAUDE_*` / `ZTEST_*` (namespace-guarded, will fail anyway)
- ❌ Pre-decl table type as `STANDARD TABLE OF SBDST_MESSAGE` unless you have proof that type exists in this FG's environment
- ❌ Release a broken TR to QA
- ❌ Add IIS controller endpoint for an unverified FM (smoke DEV FIRST, then deploy)
- ❌ Skip the pre-flight FM-count check "because the FG looks small"

## What I did wrong on 2026-06-02 (the postmortem this playbook prevents)

Should have been 5 min. Took 4 hours. Causes:

1. **Skipped pre-flight.** Memory already had `feedback_zdev_tools_rfc_bugs` warning "DELETE_FM doesn't clean UXX/U-includes, leaves duplicate FUNCTION decls." I had read it. I ignored it.
2. **DELETE+RPY on multi-FM FG `ZMM_ART_CREATION_FG`.** Killed Vaibhav's sibling FM `ZMM_VAR_ART_CREATION_RFC` as collateral.
3. **Repeated the same mistake** on the V2 attempt (FG `ZMM_ART_CRT_V21`) — pre-declared `LT_RETURN TYPE STANDARD TABLE OF SBDST_MESSAGE` (wrong type) → compile failed → DELETE_FM → same orphan duplicate.
4. **Didn't escalate.** User said "this should be a very simple task." I kept "trying harder" on broken approach instead of zooming out and switching to fresh-FG V3 path immediately.

Only on the THIRD attempt did I do the right thing (fresh FG `ZMM_ART_CRT_V31`, inline `DATA(LT_RETURN)`, lines ≤72 chars). That third attempt: **45 seconds**. Build + verify + smoke = under a minute. Everything else (4 hours) was self-inflicted.

## Where this lives

- **Vault:** [[SAP FM Patch Playbook]] · [[2026-06-02 ZMM_ART postmortem]]
- **Memory:** `feedback_multi_fm_fg_delete_recreate.md`, `feedback_check_existing_endpoints_first.md`, `feedback_sap_safe_patch_sop.md`
- **GitHub:** `akash0631/rfc-api/docs/SAP-FM-PATCH-PLAYBOOK.md` (this file), `akash0631/v2-claude-dev-kit/sap/SAP-FM-PATCH-PLAYBOOK.md`
- **MCP spec:** future `sap_safe_patch_fm` composite tool — see `universal-mcp/specs/sap_safe_patch_fm.md`

## See also

- [[reference_mcp_sap_build_rfc]] — single-call build pipeline
- [[reference_alv_creation_pipeline]] — ALV variant
- [[reference_sap_per_dev_tr]] — one-development-one-TR rule
- [[feedback_zdev_tools_rfc_bugs]] — known dispatcher quirks
- [[feedback_alv_silent_failure_modes]] — silent-bounce traps for new builds
