# RFC API — Overview

REST API for SAP RFC function modules. Runs on IIS (.NET 4.8) on Server .36.

## Architecture

```
Browser / HHT / Apps → sap-api.v2retail.net → Cloudflare Tunnel → IIS Server .36 → SAP
```

## Contents

- **194 RFC endpoints** (per live swagger) — each wraps one SAP function module
- **AbapStudioController** — SAP bridge for ABAP AI Studio (query, source, deploy)
- **GenericRfcProxy** — dynamic RFC caller for any function module
- **deploy-iis.yml** — auto-deploys on push to `Controllers/**`
- **Cloudflare Workers** — `v2-rfc-pipeline`, `v2-sql-analyst`, `v2-sql-studio` (in `workers/`)
- **Portal** — https://v2-rfc-portal.pages.dev (RFC hub explorer)
- **wrangler.toml** — deploys `v2-rfc-pipeline` worker to `sap-api.v2retail.net/pipeline*`
- **KV**: `RFC_JOBS` namespace (`f31b07a159dc4c3bbc2c06dc2c9fdafc`)

## Controller Categories

`Authentication`, `DcRouting`, `FMS_FABRIC_PUTWAY`, `Finance`, `GateEntry_LOT_Putway`, `Generic`, `HRMS`, `HU_Creation`, `HU_PRINT`, `HU_SCAN`, `Inbound`, `BroaderMenu`, `MM`, `NSO`, `Claude`, `DataV2`

## Deploy Rules

```
NEVER use workflow_dispatch
NEVER deploy to .46
Default: DEV SAP (.174, Client 210)
Push to Controllers/** → GitHub Actions deploy-iis.yml → IIS Server .36
```

## Endpoints

- **Production:** https://sap-api.v2retail.net
- **Swagger UI (canonical endpoint list):** https://sap-api.v2retail.net/swagger/ui/index — 194 paths live
- **Swagger JSON:** https://sap-api.v2retail.net/swagger/docs/v1
- **Portal:** https://v2-rfc-portal.pages.dev
- **Pipeline Worker:** https://sap-api.v2retail.net/pipeline*

## SAP Environments

| Env  | IP              | Client | Param          |
|------|-----------------|--------|----------------|
| DEV  | 192.168.144.174 | 210    | (default)      |
| QA   | 192.168.144.179 | 600    | `?env=qa`      |
| PROD | 192.168.144.170 | 600    | `?env=prod`    |

## Notable Endpoints (hardcoded env binding)

Endpoints below ignore `?env=` and bind to one SAP env in their controller via `BaseController.rfcConfigparameters{production|quality}()`. To change env, edit the controller and push to `master`.

| Endpoint | Controller | Bound Env | Live Since | Notes |
|----------|------------|-----------|------------|-------|
| `POST /api/ZMM_PO_CREATION_RFC` | [`Controllers/MM/ZMM_PO_CREATION_RFCController.cs`](../Controllers/MM/ZMM_PO_CREATION_RFCController.cs) | PROD (.170 / 600 / PRD) | 2026-05-25 (master `561c3f8`) | PO Creator app. SemaphoreSlim gate (60s). Field: `IT_ITEMS[].QTY` (not `QUANTITY`). ALPHA/MATN1 handled inside FM — see [SAP-RFC-ALPHA-CONVERSION.md](./SAP-RFC-ALPHA-CONVERSION.md). Verify env via ELSE casing: PROD lowercase `" Please provide all mandatory inputs"`, QA uppercase. |

## Configuration

- `Web.config` — SAP host, client, sysnum, pool settings
- `dab-config.json` — Data API Builder config
- Secrets: `ANTHROPIC_API_KEY`, `GITHUB_TOKEN` (as wrangler secrets)

## Related Docs

- [PO-CREATOR-API.md](./PO-CREATOR-API.md) — V2 PO Creator app integration
- [SAP-RFC-ALPHA-CONVERSION.md](./SAP-RFC-ALPHA-CONVERSION.md) — ALPHA/MATN1 internal-format gotcha
- [SAP-SNOWFLAKE-PIPELINE.md](./SAP-SNOWFLAKE-PIPELINE.md) — Bronze sync pipeline
- [SAFEGUARDS.md](./SAFEGUARDS.md) — overload protection in `/sync`
- [changelog/](./changelog/) — dated change notes
