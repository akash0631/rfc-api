# ZART_CHAR_PATCH + ZMM_ART — Master Session Handover

**Last session:** 2026-06-02 (afternoon — V3 ship + STMS-blocked + session wiped)
**Next session entry:** read this top-to-bottom, then `[[project_zart_char_patch]]` + `[[project_zmm_art_crt_v3]]` memories.

---

## 🟢 LIVE on DEV (verified end-to-end with real DB writes)

### ZART article PATCH (V3 — class-aware, CABN-validated)

| Layer | Artifact |
|---|---|
| FM | `Z_ART_PATCH_RFC_V3` in FG `ZARTPATV3`, include `LZARTPATV3U01` |
| Foundation FMs (pre-V3, still used) | `Z_ART_SCHEMA_RFC`, `Z_ART_READ_RFC` in FG `ZART_CHAR_PATCH2` (TR `S4DK925570`) |
| TR | `S4DK925580` (V3) — **STATUS R** (released, in S4Q queue) |
| API endpoint | `POST sap-api.v2retail.net/api/article/patch?env=dev` (commit `bd37a5d` master) |
| Controller | `Controllers/MM/ArticleCharController.cs` calls `Z_ART_PATCH_RFC_V3` |
| Smoke (8/8 PASS) | `M_FIT='API_E2E'` written to ZCT04, `F_WIDTH='60'` written to AUSP ATINN 0000000979, lock/unknown/not-in-mara rejects all proper |

V3 vs prior versions:
- V3 skips `BAPI_OBJCL_CHANGE` when MATNR has no class (KSSK MAFID='O', KLART='001'); only updates ZCT04 mirror. Returns `class:"(no_class)"`.
- V3 validates ATNAM via CABN (any classifiable char) OR ZCT04 component (M_* mirror). Mixed payloads work.
- V3 supersedes V1 (`Z_ART_PATCH_RFC`) and V2 (`Z_ART_PATCH_RFC_V2`). V1+V2 FMs sit unused on DEV — no caller, harmless dead code.

### ZMM article CREATION (V3 — Bug 1+2 patched, shipped this morning)

| Layer | Artifact |
|---|---|
| FM | `ZMM_ART_CRT_V3` in FG `ZMM_ART_CRT_V31` |
| TR | `S4DK925567` (released + imported to QA via Akash manual click this morning) |
| API endpoint | `POST sap-api.v2retail.net/api/ZMM_ART_CREATION_RFC?env=qa` (commit `0a75681`) |
| QA evidence | Article `1110116329` created via QA smoke 2026-06-02 morning |

### Generic Z_CLAUDE_* wrappers shipped this afternoon

| Wrapper | FG | TR | Solves |
|---|---|---|---|
| `Z_CLAUDE_FUGR_DELETE` | `ZCLAUDE_FUGR_D1` | `S4DK925576` (R) | drop any FG headless w/o REF param NCo issues — RS_FUNCTION_POOL_DELETE wrapper |
| `Z_CLAUDE_TR_RELEASE` | `ZCLAUDE_TR_R1` | `S4DK925576` (R) | release any TR w/o ES_REQUEST NCo issues — TR_RELEASE_REQUEST wrapper. **HAS BUG — see Blockers** |

---

## 🔴 BLOCKERS for next session

### Blocker 1 — `Z_CLAUDE_TR_RELEASE` returns `ok:true` but parent TR stays D

**Symptom:** Calling `Z_CLAUDE_TR_RELEASE IV_TRKORR=S4DK925570` returns `{"ok":true,"trkorr":"S4DK925570"}`. Sub-task `S4DK925571` does flip to status `R`. But parent `S4DK925570` stays at status `D` (modifiable) and never reaches QA queue.

**Root cause hypothesis:** Two possible:
1. SAP `TR_RELEASE_REQUEST` on a parent with already-released sub-tasks may require different sequence (e.g. set the parent's content lock first via another FM). Wrapper missing a prerequisite call.
2. Wrapper's `sy-subrc=0` check fires before SAP actually commits the release — async semantics. Need to add `BAPI_TRANSACTION_COMMIT WAIT='X'` or read E070 status post-call to confirm.

**Fix path next session:**
- Read SAP source for `TR_RELEASE_REQUEST` to find the actual commit + parent-release sequence
- Add a post-release E070 status read inside the wrapper, return real status not the subrc
- Test against a fresh TR built explicitly for this fix
- OR — for now, manual SE10 click is the fallback (Akash 30 sec)

### Blocker 2 — DEV `ZMM_ART_CREATION_FG` still broken

**State:** Sibling FM `ZMM_VAR_ART_CREATION_RFC` returns `"Syntax error in program SAPLZMM_ART_CREATION_FG"` on DEV. Cause from morning session = DELETE_FM + sap_rpy_create_fm on multi-FM FG → FGUXX not regenerated → orphan U01 + new U03 → duplicate FUNCTION decl. QA unaffected (we built V3 in a fresh FG `ZMM_ART_CRT_V31`).

**TR `S4DK925560`** contains exactly this broken FG. **DO NOT release/import to QA as-is** — would break QA the same way.

**Fix paths next session:**
- (a) Akash 30-sec SE80 → FG `ZMM_ART_CREATION_FG` → Activate cascade → regen FGUXX → drop orphan U01 from include list → clean. THEN release S4DK925560 safely.
- (b) Make a TOC (Transport of Copies) from S4DK925560 containing ONLY the V3 FM objects (not the broken FG state) → release TOC to QA.
- (c) Just DON'T release S4DK925560; leave DEV broken for now (QA's article creation via `ZMM_ART_CRT_V3` works anyway).

Akash mentioned "we did toc before" — clarify whether the TOC TR# is in S4Q queue under a different number than S4DK925560.

### Blocker 3 — STMS import to QA blocked by session wipe

**What I did wrong:** Used PS `[System.Windows.Forms.SendKeys]::SendWait("/nSTMS_QUEUES{ENTER}")` after `SetForegroundWindow` when SAP GUI scripting okcd became non-responsive. SendKeys typed into the wrong window. Result: Akash's 3 active SAP sessions (SE80 + SMEN + SE09) ALL LOST. Single session at S000 logon with blank user. Lesson saved to [[feedback-sendkeys-no-focus-check]] — never SendKeys to SAP without verified focus; always prefer COM scripting; never type `/n*` blindly.

**Status now:** Akash needs to re-login. Once back in, manual STMS import path = 90 sec total for 3 TRs (`S4DK925570` + `S4DK925576` + `S4DK925580`). See punch `~/claude/exports/STMS_IMPORT_3TRs_TO_QA.md`.

---

## 📋 NEXT-SESSION RESUME SEQUENCE

Run in order:

1. **Verify DEV state:**
   ```
   abap_test_fm ZMM_VAR_ART_CREATION_RFC env=dev  → still syntax error? (yes = Blocker 2 unresolved)
   sap_verify_fm Z_ART_PATCH_RFC_V3              → expect ok:true rfc_params:5
   sap_verify_fm Z_CLAUDE_FUGR_DELETE             → expect ok:true rfc_params:2
   sap_verify_fm Z_CLAUDE_TR_RELEASE              → expect ok:true rfc_params:2
   ```

2. **Verify TR states:**
   ```
   RFC_READ_TABLE E070 WHERE TRKORR IN ('S4DK925570','S4DK925576','S4DK925580','S4DK925560')
   ```
   Expect:
   - S4DK925576, S4DK925580, S4DK925560: `R` (in S4Q queue, importable)
   - S4DK925570: `D` (still bug — blocker 1)

3. **Ask Akash:**
   - Did you do the 90-sec manual STMS import of S4DK925576 + S4DK925580? (S4DK925570 stuck D until blocker 1 fixed)
   - What's the TOC TR# for S4DK925560 if you did one? (Blocker 2)

4. **Per Akash's answer:**
   - If imported → run QA smoke (commands below) + use `Z_CLAUDE_FUGR_DELETE` to drop V1 FG `ZART_CHAR_PATCH2` + V2 FG `ZART_PATCH_V21` on QA (Plan C cleanup)
   - If not imported → fix Blocker 1 wrapper bug first OR escalate to manual SE10 click
   - For S4DK925570 — either fix wrapper OR Akash manually releases via SE10

5. **QA smoke (post-import):**
   ```bash
   # Recycle IIS NCo metadata cache
   gh workflow run deploy-iis.yml --repo akash0631/rfc-api --ref master

   # API end-to-end on QA
   curl -X POST 'sap-api.v2retail.net/api/article/char-schema?env=qa'
   curl -X GET  'sap-api.v2retail.net/api/article/000000001110000044?env=qa'
   curl -X POST 'sap-api.v2retail.net/api/article/patch?env=qa' \
     -H 'Content-Type: application/json' \
     -d '{"matnr":"000000001110000044","changes":{"M_FIT":"QA_SMOKE"},"test_mode":false,"user":"AKASH"}'
   # Expect: ok:true, applied:1, class:(no_class) — ZCT04 mirror updated

   curl -X POST 'sap-api.v2retail.net/api/article/patch?env=qa' \
     -d '{"matnr":"<classified MATNR on QA>","changes":{"F_WIDTH":"60"},"test_mode":false,"user":"AKASH"}'
   # Expect: ok:true, applied:1, class:F_WIDTH — AUSP updated
   ```

6. **Plan C cleanup on QA (after smoke confirms V3 works):**
   ```bash
   # Drop V1 FG (after Blocker 1 unblocked + S4DK925570 imported)
   curl -X POST 'sap-api.v2retail.net/api/rfc/proxy?env=qa' \
     -H 'X-RFC-Key: v2-rfc-proxy-2026' \
     -d '{"bapiname":"Z_CLAUDE_FUGR_DELETE","IV_FG_NAME":"ZART_CHAR_PATCH2"}'

   # Drop V2 FG
   curl -X POST 'sap-api.v2retail.net/api/rfc/proxy?env=qa' \
     -H 'X-RFC-Key: v2-rfc-proxy-2026' \
     -d '{"bapiname":"Z_CLAUDE_FUGR_DELETE","IV_FG_NAME":"ZART_PATCH_V21"}'
   ```

   ⚠️ Don't drop `ZART_CHAR_PATCH2` if Schema/Read FMs are still in there — V3 only replaced the PATCH FM. Schema + Read live in `ZART_CHAR_PATCH2`. Either:
   - Move them to V3's FG too (rebuild)
   - Or just drop V2's FG (`ZART_PATCH_V21`) which contains only the obsolete V2 patch FM

   Decide before deleting.

---

## ⚙️ TR map (final state EOD 2026-06-02)

| TR | Status | Sub-task | Contents | Next |
|---|---|---|---|---|
| `S4DK925560` | R | S4DK925561 (R) | BROKEN `ZMM_ART_CREATION_FG` (orphan U01 + duplicate FUNCTION) | DO NOT import as-is — fix DEV first or skip |
| `S4DK925567` | R | — | `ZMM_ART_CRT_V3` (article creation) | ✅ already imported to QA |
| `S4DK925570` | **D** | S4DK925571 (R) | Foundation: 5 DTELs + 3 TABLs + FGs ZART_CHAR_PATCH1 (deleted) + ZART_CHAR_PATCH2 (V1 FMs) | **STUCK D — Blocker 1 wrapper bug. Akash manual SE10 release OR fix wrapper** |
| `S4DK925576` | R | S4DK925577 (R) | V2 patch FM + wrappers Z_CLAUDE_FUGR_DELETE + Z_CLAUDE_TR_RELEASE | ready for QA import |
| `S4DK925580` | R | S4DK925581 (R) | V3 FM `Z_ART_PATCH_RFC_V3` in FG `ZARTPATV3` | ready for QA import |

---

## 🔧 IIS / API state

- Repo: `akash0631/rfc-api` master
- Latest commit: `bd37a5d` "fix(mm): point /api/article/patch at Z_ART_PATCH_RFC_V3 (full fix)"
- Last deploy: GHA run `26807473836` success (deployed V3 controller flip)
- PROD hard-blocked: `ResolveEnv()` returns 403 for env=prod
- API endpoints LIVE on DEV: `/api/article/char-schema`, `/api/article/{matnr}`, `/api/article/patch`
- Z_CLAUDE wrappers accessible via `/api/rfc/proxy` with `X-RFC-Key: v2-rfc-proxy-2026`

---

## 📚 Memory + vault index (everything from this session)

| Note | Location |
|---|---|
| ZART_CHAR_PATCH project | `[[ZART_CHAR_PATCH]]` (vault projects/) + `project_zart_char_patch.md` (memory) |
| ZMM_ART_CRT_V3 project | `[[ZMM_ART_CRT_V3]]` + `project_zmm_art_crt_v3.md` |
| Session handover (this doc) | `~/claude/exports/article_patch_api/SESSION_HANDOVER.md` + `[[ZART_CHAR_PATCH Session Handover]]` |
| STMS import punch | `~/claude/exports/STMS_IMPORT_3TRs_TO_QA.md` |
| Z_CLAUDE_* wrapper catalog | `[[z-claude-wrapper-fms]]` + `reference_z_claude_wrapper_fms.md` |
| SAP FM Patch Playbook | `[[SAP FM Patch Playbook]]` (vault v2retail/) + GitHub `akash0631/rfc-api/docs/SAP-FM-PATCH-PLAYBOOK.md` |
| Multi-FM FG DELETE_FM trap | `[[multi-fm-fg-delete-recreate]]` |
| BAPI TABLES all required | `[[bapi-tables-all-required]]` |
| SendKeys broke session | `[[feedback-sendkeys-no-focus-check]]` (new this session) |
| Zoom-out lesson | `[[zoom-out-on-simple-task]]` |
| 2026-06-02 ZMM_ART postmortem | `[[2026-06-02 ZMM_ART postmortem]]` |
| Daily log | `daily/2026-06-02.md` appended |

---

## 🧭 Decision questions for Akash (next session)

1. **S4DK925560 — what to do?** (a) Fix DEV FGUXX manually, then release. (b) Use TOC TR# you mentioned (need TR#). (c) Skip — leave broken on DEV, QA already has V3 anyway.
2. **S4DK925570 — manual SE10 release while wrapper bug pending?** Or wait for wrapper fix.
3. **QA cleanup (Plan C) — drop V1+V2 FGs after V3 import?** Or keep them as dead code "just in case"?
4. **PROD enablement timeline for ZART_CHAR_PATCH** — when?
5. **App routes** — confirm po-wise-wardrobe gets `/article/:matnr/edit` route + new article-mod app shares OpenAPI types?
6. **/api/article/allowed-values endpoint** — for CAWN dropdown picker in app form? (was Decision Q4 from prior session — still open)

---

## 🚨 Anti-patterns (don't repeat — saved as feedback memories)

1. **`DELETE_FM` in multi-FM FG** → orphan U-include + duplicate FUNCTION → pool-wide syntax error. Use fresh-FG V<N> instead. ([[feedback-multi-fm-fg-delete-recreate]], [[SAP FM Patch Playbook]])
2. **`SendKeys` to SAP GUI w/o focus verification** → wipes all sessions. NEVER. Always COM scripting. ([[feedback-sendkeys-no-focus-check]])
3. **`Z_CLAUDE_TR_RELEASE` wrapper bug — `ok:true` lies** → check E070 status after release call, don't trust subrc.
4. **`sap_build_rfc` "no free suffix 1-99"** when fg_base substring matches existing FG list → use `fg_name` (exact) instead of `fg_base` (auto-suffix).
5. **Releasing TRs containing known-broken FGs** → imports the brokenness to QA. Pre-check `abap_test_fm` on all FMs in the FG before release.
6. **Classic ABAP `CALL FUNCTION ... TABLES`** must bind ALL declared TABLES params even when empty. "Missing parameter in CALL FUNCTION." often = missing TABLES, not missing IMPORT. ([[bapi-tables-all-required]])

---

## 🏁 If you're a fresh agent reading this

**Start here:**
1. Read this whole file
2. Read [[project_zart_char_patch]]
3. Run step 1+2 from "NEXT-SESSION RESUME SEQUENCE" above to probe state
4. Ask Akash the 6 decision questions
5. Execute per his answers

**Don't:**
- Touch existing multi-FM FGs with DELETE_FM
- SendKeys to SAP GUI
- Trust `Z_CLAUDE_TR_RELEASE` ok:true — verify via E070 post-release
- Release TRs containing FGs without compile check
- "Try harder" when Akash says "should be simple" — restart with fresh architecture

**Do:**
- Use `Z_CLAUDE_FUGR_DELETE` to drop any FG headless
- Use fresh-FG V<N> path for FM patches (see [[SAP FM Patch Playbook]])
- Pre-flight `RFC_READ_TABLE TFDIR WHERE PNAME='SAPL<FG>'` row count
- ABAP source: ≤72-col, no FUNCTION wrapper, inline `DATA()`, no unnecessary TRY/CATCH or COND

End handover.
