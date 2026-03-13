# v2rfc — V2 Retail RFC Pipeline CLI

Upload an SAP RFC `.docx` document and automatically:
1. **Parse** the RFC spec (import params, output table, description)
2. **Generate** the ASP.NET C# controller
3. **Generate** the .NET data pipeline project
4. **Push** controller to GitHub → IIS auto-deploys
5. **Register** the SQL table in DAB config → Azure REST API goes live
6. **Update** the Swagger documentation portal

---

## Install

```bash
cd cli/
npm install
npm link        # makes 'v2rfc' available globally
# OR just run directly:
node bin/v2rfc.js deploy MyRFC.docx
```

Set your Anthropic API key:
```bash
export ANTHROPIC_API_KEY=sk-ant-...
```

---

## Commands

### `v2rfc deploy <file.docx>`
Full pipeline — parse, generate, push everything.

```bash
v2rfc deploy ZMY_RFC.docx                     # deploy to Dev (default)
v2rfc deploy ZMY_RFC.docx --env quality       # deploy to Quality
v2rfc deploy ZMY_RFC.docx --env production    # deploy to Production
v2rfc deploy ZMY_RFC.docx --no-pipeline       # skip data pipeline generation
v2rfc deploy ZMY_RFC.docx --no-swagger        # skip swagger doc update
```

### `v2rfc parse <file.docx>`
Dry run — show what was extracted from the document (no deploy).

```bash
v2rfc parse ZMY_RFC.docx
```

### `v2rfc status`
Show all registered DAB entities and portal URLs.

### `v2rfc dab-list`
List all 74+ entities in dab-config.json with their SQL table names.

### `v2rfc swagger`
Print all API URLs (Swagger UI, DAB base, OpenAPI JSON).

### `v2rfc env [name]`
Show SAP environment details.

```bash
v2rfc env           # list all environments
v2rfc env dev       # show dev details
v2rfc env production
```

---

## What gets deployed

| Step | Where | URL |
|------|-------|-----|
| Controller code | `akash0631/rfc-api` | GitHub → IIS (V2DC-ADDVERB) |
| RFC API endpoint | IIS on V2DC-ADDVERB | `POST /api/{RFC_NAME}` |
| Data pipeline project | `akash0631/rfc-api/DataPipelines/` | Run .exe to load SQL |
| DAB entity | `dab-config.json` → Azure | `GET https://my-dab-app.azurewebsites.net/api/{TABLE}` |
| Swagger card | `v2_sap_api_explorer.html` | `https://v2-rfc-portal.pages.dev/swagger` |

---

## Architecture

```
SAP RFC .docx
     ↓  (v2rfc deploy)
  Claude parses RFC spec
     ↓
  C# Controller + .NET Pipeline generated
     ↓
  GitHub: akash0631/rfc-api
  ├── Controllers/{Category}/{RFC}Controller.cs   → IIS auto-deploy
  ├── DataPipelines/{RFC}/Program.cs              → SQL Server data lake
  ├── dab-config.json                             → Azure DAB restart
  └── v2_sap_api_explorer.html                    → Swagger portal
     ↓
  [Run Pipeline.exe daily via Task Scheduler]
     ↓
  dbo.{TABLE} populated in SQL Server (Azure VPN → on-prem 192.168.144.x)
     ↓
  GET https://my-dab-app.azurewebsites.net/api/{TABLE}
  → Internal apps consume the data via REST + OData filters
```

---

## SAP Environments

| Env | Host | Client | Use for |
|-----|------|--------|---------|
| dev | 192.168.144.174 | 210 | All new RFCs (default) |
| quality | 192.168.144.179 | 600 | Testing before prod |
| production | 192.168.144.170 | 600 | Live after sign-off |

**Default is always `dev`** — you must explicitly pass `--env production` to target prod.

---

## Portal Links

| Link | Purpose |
|------|---------|
| https://v2-rfc-portal.pages.dev | RFC Automator (web UI alternative) |
| https://v2-rfc-portal.pages.dev/swagger | Live Swagger UI |
| https://my-dab-app.azurewebsites.net/api/openapi | OpenAPI JSON spec |
