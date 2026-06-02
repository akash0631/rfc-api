# MCP Tool Spec — `sap_safe_patch_fm`

> Composite tool that does the right thing for "patch an existing SAP FM" without the orphan-include trap. Wraps `sap_dispatcher` + `sap_rpy_create_fm` + `sap_build_rfc` + `RFC_READ_TABLE` + IIS-controller-hint emit. Source of truth: [[SAP FM Patch Playbook]].

## Why this tool

Without this composite, the LLM has to chain 6+ tool calls AND remember the multi-FM FG rule. We've seen this fail (2026-06-02 ZMM_ART spiral). One callable that encodes the rule → no more rope to hang yourself with.

## Tool surface

```typescript
{
  name: "sap_safe_patch_fm",
  description: "Patch an existing SAP RFC function module headless. Pre-flights FM count in the target FG to decide between in-place patch (single-FM FG) vs fresh-FG V<N> path (multi-FM FG). Avoids the DELETE_FM-orphan-include trap. Returns the active FM name and IIS controller swap hint.",

  parameters: {
    fm_name: { type: "string", required: true,
      description: "Existing FM to patch, e.g. 'ZMM_ART_CREATION_RFC'." },
    short_text: { type: "string", required: true,
      description: "Description for the new FM / TR." },
    patches: { type: "array", required: true,
      description: "Ordered list of source patches to apply against current FM source.",
      items: {
        type: { enum: ["replace", "insert_after", "insert_before"] },
        anchor: "string  // exact substring or regex from current source",
        text: "string    // new ABAP text (no FUNCTION wrapper, ≤72 cols)"
      }
    },
    dev_tr: { type: "string", required: false,
      description: "Existing dev TR to reuse. If absent, mints a new one." },
    naming_suffix: { type: "string", default: "_V",
      description: "Suffix added before the numeric version for fresh-FG path." }
  }
}
```

## Return shape

```json
{
  "status": "ok" | "compile_failed" | "blocked",
  "path_taken": "in_place" | "fresh_fg",
  "fm_name_used": "ZMM_ART_CRT_V3",
  "fg_used": "ZMM_ART_CRT_V31",
  "include_used": "LZMM_ART_CRT_V31U01",
  "tr": "S4DK925567",
  "dev_tr": "S4DK925567",
  "orig_fm_name": "ZMM_ART_CREATION_RFC",
  "orig_fg": "ZMM_ART_CREATION_FG",
  "fm_count_in_orig_fg": 2,
  "decision_reason": "Multi-FM FG (2 FMs) — DELETE_FM unsafe; used fresh-FG path",
  "patched_source": "<full patched ABAP body, no FUNCTION wrapper>",
  "smoke": { "syntax_error": false, "EX_RETURN": {...} },
  "iis_controller_hint": "Edit Controllers/<area>/<OrigFm>Controller.cs line ~89: dest.Repository.CreateFunction(\"ZMM_ART_CRT_V3\")  // was: ZMM_ART_CREATION_RFC",
  "release_steps": ["sap_dispatcher RELEASE_TR <child_task>", "sap_dispatcher RELEASE_TR <dev_tr>"],
  "stms_import_hint": "Manual: /nSTMS_QUEUES → double-click S4Q → Ctrl+F TR → =IMPS → confirm. OR VBS at ~/claude/tmp/sap_stms_drive_v2.vbs"
}
```

## Logic flow

```
1. RESOLVE original FG:
   tfdir = RFC_READ_TABLE TFDIR WHERE FUNCNAME = '<fm_name>'
   orig_fg_name = strip("SAPL" prefix) from tfdir.PNAME
   if not found → return status:"blocked", reason:"FM does not exist"

2. COUNT FMs in original FG:
   fm_count = RFC_READ_TABLE TFDIR WHERE PNAME='SAPL<orig_fg>' count
   (excluding deleted/inactive)

3. READ current source:
   src = abap_read_source(fm_name)

4. APPLY patches:
   patched_src = applyPatches(src, patches)
   validateLines72(patched_src)
   stripFunctionWrapper(patched_src)

5. CHOOSE PATH:
   if fm_count == 1:
     path = "in_place"
     // DELETE+RECREATE same name same FG is safe (single-FM FG)
   else:
     path = "fresh_fg"
     new_fm = next available "<fm_name>_V<N>" (probe TFDIR for next free)
     new_fg_base = new_fm
     // sap_build_rfc auto-suffixes FG to <new_fm>1, <new_fm>2 etc.

6. EXECUTE:
   if path == "in_place":
     sap_dispatcher DELETE_FM IM_FM_NAME=<fm_name>, dev_tr=<dev_tr>
     sap_rpy_create_fm fm_name=<fm_name>, fg_name=<orig_fg>, ...
   else:
     sap_build_rfc fm_name=<new_fm>, fg_base=<new_fm>, body=<patched_src>, ...

7. VERIFY:
   sap_verify_fm(active_fm_name) → expect ok:true
   abap_test_fm(active_fm_name) → expect syntax_error:false

8. RETURN with controller_hint + release_steps + stms_import_hint
```

## Why this is safer than today

Today, the LLM must:
- remember `feedback_zdev_tools_rfc_bugs` warning
- remember to pre-check FM count
- choose between `sap_dispatcher DELETE_FM` + `sap_rpy_create_fm` (in-place) vs `sap_build_rfc` (fresh-FG)
- format ABAP source for 72-col + no-FUNCTION rules
- generate the right name suffix
- emit IIS controller swap line

In a 4-hour session it forgot 4 of 6.

With this tool, the rule is in code. The LLM gives the patch list; the tool chooses the right path.

## Reference implementation (drop into `index.js`)

```javascript
// === sap_safe_patch_fm — composite safe FM patch ===
{
  name: "sap_safe_patch_fm",
  description: "Safe FM patch: pre-checks FG FM count, picks in-place (single-FM FG) vs fresh-FG V<N> path automatically. Avoids DELETE_FM orphan-include trap. Returns active FM, IIS controller hint, release+import steps.",
  inputSchema: { /* see spec above */ },
  handler: async (params, ctx) => {
    const { fm_name, short_text, patches, dev_tr: existing_tr, naming_suffix = "_V" } = params;

    // 1. Resolve original FG
    const tfdirResp = await callProxy(ctx, "RFC_READ_TABLE", {
      QUERY_TABLE: "TFDIR",
      OPTIONS: [{ TEXT: `FUNCNAME = '${fm_name}'` }],
      FIELDS: [{ FIELDNAME: "PNAME" }]
    });
    const orig_fg_pname = tfdirResp.DATA?.[0]?.WA?.trim();
    if (!orig_fg_pname) return { status: "blocked", reason: `FM ${fm_name} not in TFDIR` };
    const orig_fg = orig_fg_pname.replace(/^SAPL/, "").trim();

    // 2. Count FMs in FG
    const countResp = await callProxy(ctx, "RFC_READ_TABLE", {
      QUERY_TABLE: "TFDIR",
      OPTIONS: [{ TEXT: `PNAME = '${orig_fg_pname}'` }],
      FIELDS: [{ FIELDNAME: "FUNCNAME" }]
    });
    const fm_count = (countResp.DATA || []).length;

    // 3. Read source
    const srcResp = await callAbap(ctx, "abap_read_source", { fm_name });
    let src = srcResp.source.replace(/^FUNCTION\s+\S+\s*\.\s*\n/i, "")
                            .replace(/\nENDFUNCTION\.\s*$/i, "");
    // strip auto-generated interface block (lines starting with *")
    src = src.split("\n").filter(l => !l.match(/^\*"/)).join("\n");

    // 4. Apply patches
    for (const p of patches) {
      if (p.type === "replace") {
        if (!src.includes(p.anchor))
          return { status: "blocked", reason: `replace anchor not found: ${p.anchor.slice(0,60)}` };
        src = src.replace(p.anchor, p.text);
      } else if (p.type === "insert_after") {
        src = src.replace(p.anchor, p.anchor + "\n" + p.text);
      } else if (p.type === "insert_before") {
        src = src.replace(p.anchor, p.text + "\n" + p.anchor);
      }
    }

    // 5. Validate 72-col
    const overLong = src.split("\n").findIndex(l => l.length > 72);
    if (overLong !== -1)
      return { status: "compile_failed", reason: `line ${overLong+1} exceeds 72 chars`, patched_source: src };

    // 6. Build interface from current FM
    const ifaceResp = await callAbap(ctx, "abap_read_interface", { fm_name });
    const imports = []; const exports = [];
    for (const wa of (ifaceResp.DATA || []).map(d => d.WA)) {
      const [paramType, paramName, struct] = [wa[0], wa.slice(2,32).trim(), wa.slice(33,165).trim()];
      const entry = { PARAMETER: paramName, TYP: struct, PASS_VALUE: "X", DEFAULT: "" };
      if (paramType === "I") imports.push(entry);
      else if (paramType === "E") exports.push(entry);
    }

    // 7. Choose path
    if (fm_count <= 1) {
      // in-place safe
      const tr = existing_tr || (await callDispatcher(ctx, "CREATE_TR", { IM_SHORT_TEXT: short_text })).EX_TR_NUMBER;
      await callDispatcher(ctx, "DELETE_FM", { IM_FM_NAME: fm_name }, tr);
      const createResp = await callRpyCreateFm(ctx, { fm_name, fg_name: orig_fg, short_text, dev_tr: tr, remote: true, imports, exports, source_lines: src.split("\n") });
      const verify = await callAbap(ctx, "sap_verify_fm", { fm_name });
      const smoke = await callAbap(ctx, "abap_test_fm", { fm_name, env: "dev" });
      return {
        status: smoke.syntax_error ? "compile_failed" : "ok",
        path_taken: "in_place",
        fm_name_used: fm_name,
        fg_used: orig_fg,
        include_used: createResp.FUNCTION_INCLUDE,
        tr, dev_tr: tr,
        orig_fm_name: fm_name, orig_fg, fm_count_in_orig_fg: fm_count,
        decision_reason: `Single-FM FG (${fm_count} FM) — in-place DELETE+RPY safe`,
        patched_source: src, smoke,
        iis_controller_hint: `No controller change needed — FM name unchanged.`,
        release_steps: [`RELEASE_TR child task of ${tr}`, `RELEASE_TR ${tr}`],
        stms_import_hint: "Standard STMS_QUEUES → S4Q → =IMPS"
      };
    } else {
      // fresh-FG path
      // probe next free suffix
      let newFm; let n = 2;
      while (n < 20) {
        const candidate = `${fm_name}${naming_suffix}${n}`;
        const probe = await callProxy(ctx, "RFC_READ_TABLE", {
          QUERY_TABLE: "TFDIR",
          OPTIONS: [{ TEXT: `FUNCNAME = '${candidate}'` }],
          FIELDS: [{ FIELDNAME: "FUNCNAME" }]
        });
        if (!probe.DATA || probe.DATA.length === 0) { newFm = candidate; break; }
        n++;
      }
      if (!newFm) return { status: "blocked", reason: "No free V<N> slot in range V2..V19" };

      const buildResp = await callBuildRfc(ctx, {
        fm_name: newFm, fg_base: newFm, short_text, dev_tr: existing_tr,
        imports, exports, body: src
      });
      return {
        status: buildResp.status,
        path_taken: "fresh_fg",
        fm_name_used: newFm,
        fg_used: buildResp.fg,
        include_used: buildResp.include,
        tr: buildResp.tr, dev_tr: buildResp.dev_tr,
        orig_fm_name: fm_name, orig_fg, fm_count_in_orig_fg: fm_count,
        decision_reason: `Multi-FM FG (${fm_count} FMs) — DELETE_FM forbidden; built fresh FG ${buildResp.fg}`,
        patched_source: src, smoke: buildResp.smoke,
        iis_controller_hint: `Edit Controllers/<area>/${fm_name}Controller.cs line ~89:\n  dest.Repository.CreateFunction("${newFm}")  // was: "${fm_name}"`,
        release_steps: [`RELEASE_TR child task of ${buildResp.tr}`, `RELEASE_TR ${buildResp.tr}`],
        stms_import_hint: "Manual: /nSTMS_QUEUES → double-click S4Q → Ctrl+F TR → =IMPS → confirm. OR VBS at ~/claude/tmp/sap_stms_drive_v2.vbs"
      };
    }
  }
}
```

## Deployment

Worker is at `nubo-cube-bot.akash-bab.workers.dev` (or `universal-mcp.akash-bab.workers.dev`). Per memory `[Per-Dev-TR + auto E071 registration 2026-06-01]`:

```bash
CLOUDFLARE_API_TOKEN=<CF_TOKEN> CLOUDFLARE_ACCOUNT_ID=bab06c93... \
  npx wrangler deploy
```

CF OAuth was expired as of 2026-06-01. Refresh token + deploy when ready.

## Acceptance tests

Once deployed, the 2026-06-02 ZMM_ART scenario should run as:

```javascript
sap_safe_patch_fm({
  fm_name: "ZMM_ART_CREATION_RFC",
  short_text: "Article creation Bug 1+2 patch",
  patches: [
    { type: "replace",
      anchor: "LS_VALIDATED_DATA-YEAR = SY-DATUM+0(4).",
      text: `IF <LS_ART_FINAL>-SEASON IS NOT INITIAL.\n      LS_VALIDATED_DATA-YEAR = SY-DATUM+0(4).\n    ELSE.\n      CLEAR LS_VALIDATED_DATA-YEAR.\n    ENDIF.`
    },
    { type: "replace",
      anchor: "LS_EXPORT-MESSAGE = LT_RETURN[ 2 ]-ATTR2.",
      text: `IF lines( LT_RETURN ) >= 2.\n        LS_EXPORT-MESSAGE = LT_RETURN[ 2 ]-ATTR2.\n        LS_EXPORT-SAP_ART = LT_RETURN[ 2 ]-ATTR3.\n      ENDIF.`
    }
  ]
})
// → returns: path_taken:"fresh_fg", fm_name_used:"ZMM_ART_CREATION_RFC_V2",
//            fg_used:"ZMM_ART_CREATION_RFC_V21", tr:"<new>",
//            decision_reason:"Multi-FM FG (2 FMs) — DELETE_FM forbidden..."
// 1 call, ~10 seconds.
```
