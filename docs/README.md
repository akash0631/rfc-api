# RFC API — Docs

Team-editable documentation for the `akash0631/rfc-api` repo. Edit any file here directly on GitHub or via PR — changes don't redeploy the IIS app (no `Controllers/**` touched).

## Architecture & endpoints

- **[RFC-API-OVERVIEW.md](./RFC-API-OVERVIEW.md)** — gateway architecture, SAP env table, swagger link, Notable Endpoints (PROD-bound controllers)
- **Live Swagger UI:** https://sap-api.v2retail.net/swagger/ui/index (194 paths, auto-generated)
- **Live Swagger JSON:** https://sap-api.v2retail.net/swagger/docs/v1

## Per-endpoint

- **[PO-CREATOR-API.md](./PO-CREATOR-API.md)** — `POST /api/ZMM_PO_CREATION_RFC` request/response contract, field names, error fingerprints. Upstream: Lovable PO Creator app.
- **[HHT-ARTICLE-LOOKUP-API.md](./HHT-ARTICLE-LOOKUP-API.md)** — `POST /api/article-lookup` combined Article Type + Article Size lookup for V09 → 0001 Article Putaway HHT screen. Upstream: HHT mobility app.

## SAP gotchas / lessons

- **[SAP-RFC-ALPHA-CONVERSION.md](./SAP-RFC-ALPHA-CONVERSION.md)** — ALPHA / MATN1 internal-format gotcha. Pre-deploy checklist for every new Z-FM wrapped behind the gateway.
- **[SAFEGUARDS.md](./SAFEGUARDS.md)** — SAP overload protection in `/sync`
- **[PIPELINE_LEARNINGS.md](./PIPELINE_LEARNINGS.md)** — Bronze sync lessons

## Pipelines

- **[SAP-SNOWFLAKE-PIPELINE.md](./SAP-SNOWFLAKE-PIPELINE.md)** — daily BRONZE sync flow

## Changelog

Dated change notes — append a new file each time a controller is rewired, an env binding flips, or a payload contract changes.

- [2026-05-25 — `ZMM_PO_CREATION_RFC` flipped QA → PROD](./changelog/2026-05-25-zmm-po-creation-prod-flip.md)
- [2026-05-26 — V2RfcTestPool NCo wedge incident + safeguards](./changelog/2026-05-26-v2rfctestpool-nco-wedge.md)
- [2026-05-27 — HHT Article Lookup API live](./changelog/2026-05-27-hht-article-lookup.md)

## How to edit

1. Open the file on GitHub → pencil icon → commit straight to `master` (small fixes) or open a PR for review (larger changes).
2. Or clone locally, edit, push.
3. Docs changes do NOT trigger the IIS deploy workflow (it watches `Controllers/**` only).

## Conventions

- One Markdown file per topic. No deep folder nesting except `changelog/`.
- Dates: `YYYY-MM-DD`.
- Cross-link with relative paths (`./PO-CREATOR-API.md`), not absolute URLs.
- Code blocks fenced with language tag (`abap`, `csharp`, `json`, `bash`).

- [RFC-POOL-WEDGE-RUNBOOK.md](RFC-POOL-WEDGE-RUNBOOK.md) — V2RfcTestPool NCo wedge symptom + recycle fix + watchdog details (2026-05-26)
