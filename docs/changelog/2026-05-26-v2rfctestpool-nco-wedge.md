# 2026-05-26 — V2RfcTestPool NCo wedge incident + safeguards

## Incident
- HHT DEV + QA testers blocked since 2026-05-23. Every `/api/rfc/proxy` call hung 30s on all envs.
- Root cause: NCo `RfcDestinationManager` cache corrupted inside V2RfcTestPool w3wp on V2DC-ADDVERB.
- Fix: `Restart-WebAppPool -Name V2RfcTestPool`. Recovery in 8s.
- PROD HHT unaffected (uses Azure middleware via `java-mw-9080`).

## Safeguards added
- Scheduled Task `V2-RFC-Watchdog` on V2DC-ADDVERB: probes `/api/rfc/proxy?env=dev` every 5 min, auto-recycles pool on 2 consecutive failures.
- Script: `C:\v2-watchdog\rfc-watchdog.ps1` (manage on .36 only).
- V2RfcTestPool nightly recycle 22:30 UTC, privateMemory cap 1 GB, idle timeout disabled.

## Docs
- `docs/RFC-POOL-WEDGE-RUNBOOK.md` — new runbook for this failure mode + anti-patterns.
- Obsidian vault: `v2retail/V2RfcTestPool NCo Wedge 2026-05-26.md`, `runbooks/Debug HHT.md` (updated).
