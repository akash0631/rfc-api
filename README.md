# V2 Retail — SAP RFC API

REST API for SAP RFC function modules. Runs on IIS (.NET 4.8) on Server .36.

## Architecture
```
Browser/HHT/Apps → sap-api.v2retail.net → Cloudflare Tunnel → IIS Server .36 → SAP
```

## What This Repo Contains
- **148 RFC Controllers** (.cs) — each wraps one SAP function module as a REST endpoint
- **AbapStudioController** — SAP bridge for ABAP AI Studio (query, source, deploy)
- **GenericRfcProxy** — dynamic RFC caller for any function module
- **deploy-iis.yml** — auto-deploys on push to `Controllers/**`

## What This Repo Does NOT Contain
- ABAP AI Studio frontend/worker → see [abap-ai-studio](https://github.com/akash0631/abap-ai-studio)
- Cloudflare Workers → separate repos
- SQL Analyst → separate worker

## Deploy
Push to `Controllers/**` → GitHub Actions → auto-deploy to IIS on .36
```
NEVER use workflow_dispatch
NEVER deploy to .46
Default: DEV SAP (.174, Client 210)
```

## Endpoints
- Production: https://sap-api.v2retail.net
- Swagger: https://sap-api.v2retail.net/swagger
- Portal: https://v2-rfc-portal.pages.dev

## SAP Environments
| Env  | IP              | Client | Param          |
|------|-----------------|--------|----------------|
| DEV  | 192.168.144.174 | 210    | (default)      |
| QA   | 192.168.144.179 | 600    | ?env=qa        |
| PROD | 192.168.144.170 | 600    | ?env=prod      |

## Team
Different team from ABAP AI Studio. Changes here do NOT affect abap.v2retail.net.
