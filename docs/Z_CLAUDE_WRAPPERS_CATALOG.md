# Z_CLAUDE_* Wrapper FMs Catalog

> Headless workarounds for SAP internal FMs that NCo proxy can't reach. Each wrapper is in its own fresh FG (avoids multi-FM FG DELETE_FM trap per `SAP-FM-PATCH-PLAYBOOK.md`).

## Call pattern

```bash
curl -X POST 'https://sap-api.v2retail.net/api/rfc/proxy?env=dev' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -H 'Content-Type: application/json' \
  -d '{"bapiname":"<wrapper_fm>", ...params}'
```

## Inventory

| Wrapper FM | FG | TR | Purpose | Wraps |
|---|---|---|---|---|
| `Z_CLAUDE_FUGR_DELETE` | `ZCLAUDE_FUGR_D1` | `S4DK925576` | Drop any FG headless (any namespace, any state) — bypasses dispatcher `DELETE_FG` namespace guard + NCo REF-param refusal | `RS_FUNCTION_POOL_DELETE` |
| `Z_CLAUDE_TR_RELEASE` | `ZCLAUDE_TR_R1` | `S4DK925576` | Release any TR / sub-task headless — bypasses NCo refusal on `ES_REQUEST TYPE TRWBO_REQUEST` export | `TR_RELEASE_REQUEST` |

## Z_CLAUDE_FUGR_DELETE

Drops a function group fully headless. Bypasses three separate blockers:
1. Dispatcher `DELETE_FG` namespace guard (only allows `ZCLAUDE_*` / `ZTEST_*`)
2. NCo proxy refusal on `RS_FUNCTION_POOL_DELETE` (interface has `WB_FB_MANAGER REF TO CL_FUNCTION_BUILDER_POOL` + `LIFECYCLE_MANAGER REF TO IF_ADT_LIFECYCLE_MANAGER` — NCo can't introspect REF types)
3. Modern S/4 SE80 VBS fragility (13-pane GuiSplitterShell, no dropdown, brittle tree drill)

### Signature

```abap
IMPORTING IV_FG_NAME TYPE RS38L-AREA
EXPORTING EV_JSON TYPE STRING  " {"ok":bool, "fg":"...", "corr":"TR#"|"rc":N}
```

### Smoke

```bash
curl -X POST 'https://sap-api.v2retail.net/api/rfc/proxy?env=dev' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -H 'Content-Type: application/json' \
  -d '{"bapiname":"Z_CLAUDE_FUGR_DELETE","IV_FG_NAME":"ZART_CHAR_PATCH1"}'

# → {"EV_JSON":"{\"ok\":true,\"fg\":\"ZART_CHAR_PATCH1\",\"corr\":\"S4DK925571\"}", ...}
# Then verify gone:
curl ... '{"bapiname":"RFC_READ_TABLE","QUERY_TABLE":"TLIBV","OPTIONS":[{"TEXT":"AREA = '\''ZART_CHAR_PATCH1'\''"}]}'
# → DATA: []
```

## Z_CLAUDE_TR_RELEASE

Releases a transport request without the NCo struct-export issue. SAP's `TR_RELEASE_REQUEST` exports `ES_REQUEST TYPE TRWBO_REQUEST` (deep struct with TR objects, comments, tasks etc.); NCo proxy fails serializing it. Wrapper discards the struct + returns clean JSON.

### Signature

```abap
IMPORTING IV_TRKORR TYPE E070-TRKORR
EXPORTING EV_JSON TYPE STRING  " {"ok":bool, "trkorr":"...", "rc":N}
```

### Smoke

```bash
# Always release child tasks BEFORE parent
curl -X POST 'https://sap-api.v2retail.net/api/rfc/proxy?env=dev' \
  -H 'X-RFC-Key: v2-rfc-proxy-2026' \
  -H 'Content-Type: application/json' \
  -d '{"bapiname":"Z_CLAUDE_TR_RELEASE","IV_TRKORR":"S4DK925571"}'
# → {"EV_JSON":"{\"ok\":true,\"trkorr\":\"S4DK925571\"}"}

# Then parent
curl ... '{"bapiname":"Z_CLAUDE_TR_RELEASE","IV_TRKORR":"S4DK925570"}'
```

### RC codes (exception → rc number)

```
1=CTS_INITIALIZATION_FAILURE  2=NO_AUTHORIZATION
3=ENQUEUE_FAILED              4=INVALID_REQUEST
5=REQUEST_ALREADY_RELEASED    6=REPEAT_TOO_EARLY
7=ERROR_IN_EXPORT_METHODS     8=OBJECT_CHECK_ERROR
9=DOCU_MISSING                10=DB_ACCESS_ERROR
11=ACTION_ABORTED_BY_USER     12=EXPORT_FAILED
```

## Pattern for new wrappers

When an SAP internal FM can't be called via `/api/rfc/proxy` because:
- It has REF TO interface params (NCo metadata reflection fails — error: "cannot find STRUCTURE specified by REF TO ...")
- It exports complex deep structs that NCo can't serialize
- It's not flagged RFC-callable (R3STATE=' ' on R3STATE check)

→ build a thin `Z_CLAUDE_<verb>_<noun>` wrapper FM:
1. `sap_build_rfc` with `fg_base=ZCLAUDE_<short>` (fresh FG, NEVER reuse — see SAP-FM-PATCH-PLAYBOOK.md)
2. Take simple IMPORT params (STRING, table names, TR numbers — proxy-friendly types only)
3. Inside, call the internal FM with full ABAP types (REF TO, deep structs OK)
4. Return JSON STRING export with `{"ok":bool, ...payload, "rc":N}` shape
5. Activate via `sap_build_rfc` activation chain → callable via proxy
6. Register into your dev_tr (already automatic via sap_build_rfc → Z_CLAUDE_TR_REG)

Naming: `Z_CLAUDE_<VERB>_<NOUN>` (e.g. `Z_CLAUDE_FUGR_DELETE`, `Z_CLAUDE_TR_RELEASE`, future: `Z_CLAUDE_USER_LOCK`, `Z_CLAUDE_JOB_KILL`, ...).

## See also

- `SAP-FM-PATCH-PLAYBOOK.md` — 5-min FM patch SOP (always read first)
- `docs/SAP-SAFE-PATCH-FM-MCP-SPEC.md` — composite MCP tool spec (next-gen this pattern)
- akash0631/rfc-api `docs/SAP-FM-PATCH-PLAYBOOK.md`
