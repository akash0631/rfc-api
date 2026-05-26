# V2RfcTestPool NCo Wedge — Runbook

> First documented 2026-05-26. Companion to [[Obsidian: V2RfcTestPool NCo Wedge 2026-05-26]].
> Intended audience: any developer (or their Claude Code session) touching `rfc-api` on Server .36.

## TL;DR

If `https://sap-api.v2retail.net/api/rfc/proxy` hangs for ~30 seconds on every environment (dev / qa / prod) while `https://sap-api.v2retail.net/api/health` still returns 200 in <1s, **do not chase Azure Relay, network, or SAP credentials**. The NCo destination cache inside the IIS `V2RfcTestPool` worker process is corrupted. Recycle the pool.

```powershell
# On V2DC-ADDVERB (192.168.151.36) as Administrator
Import-Module WebAdministration
Restart-WebAppPool -Name V2RfcTestPool
Start-Sleep 8
```

Verify externally:

```bash
curl -m 30 -X POST "https://sap-api.v2retail.net/api/rfc/proxy?env=dev" \
  -H "X-RFC-Key: v2-rfc-proxy-2026" \
  -H "Content-Type: application/json" \
  -d '{"bapiname":"STFC_CONNECTION","REQUTEXT":"PING"}'
# Expect: {"ECHOTEXT":"PING","RESPTEXT":"SAP R/3 Rel. 755   Sysid: S4D ..."} in <1s
```

If the curl returns within 1 second with `Sysid: S4D` (or `S4Q` for `?env=qa`, `S4P` for `?env=prod`), the pool is healed.

## How to recognize this failure mode

Three signals together = NCo wedge, not network/SAP/credentials:

1. `GET /api/health` returns 200 in <1s with `BuildDate`, `IISPool: V2RfcTestPool`, `Uptime > 0`
2. `POST /api/rfc/proxy?env=dev` with empty body returns `{"EX_RETURN":{"TYPE":"E","MESSAGE":"bapiname is required"}}` in <1s (the controller is alive, validation works)
3. `POST /api/rfc/proxy?env=<any>` with a valid `bapiname` hangs exactly 30 seconds, then 504/timeout
4. From Server .36, `Test-NetConnection 192.168.144.174 -Port 3200` and `-Port 3300` both succeed (network is fine)

If all four are true, recycle the pool. Anything else, see [Anti-patterns](#anti-patterns).

## Why this happens

The SAP .NET Connector (`sapnco_utils.dll 3.0.18.0`) loaded into the IIS w3wp process for `V2RfcTestPool` caches `RfcDestination` handles in `RfcDestinationManager`. If a destination `Open()` fails during cold start (and the bad handle isn't disposed), subsequent calls block on NCo's internal `lock(destinations)` waiting for the bad handle to be released. The lock never releases — only a fresh process clears it.

Why does the bad handle appear? Most likely sources:
- Deploy/recycle window where SAP gateway briefly rejected first connection (transport import, work-process restart, etc.)
- Network blip during w3wp cold start
- ZIP/asset extraction race inside the controller assembly

The 2026-05-23 / 2026-05-25 incident traced to the 2026-05-25 09:20 UTC deploy of `561c3f8` (ZMM_PO_CREATION_RFC PROD flip) recycling the pool with an unlucky cold start.

## Operational safeguards (live from 2026-05-26)

A self-healing watchdog runs every 5 minutes on V2DC-ADDVERB:

- **Scheduled Task** `V2-RFC-Watchdog` (runs as SYSTEM, every 5 min)
- **Script** `C:\v2-watchdog\rfc-watchdog.ps1`
- **Probe**: `POST /api/rfc/proxy?env=dev` with STFC_CONNECTION, 15s timeout
- **Threshold**: 2 consecutive failures → `Restart-WebAppPool V2RfcTestPool` → verify → log `RECYCLE_OK`
- **Log**: `C:\v2-watchdog\rfc-watchdog.log`
- **Max blackout window**: 10 minutes (one missed probe + recycle)

Additional IIS settings:
- Nightly recycle at 22:30 UTC (04:00 IST) when stores idle
- Private memory cap 1 GB
- Idle timeout disabled (so worker isn't recycled mid-day mid-request)

To check the watchdog history:

```powershell
Get-Content C:\v2-watchdog\rfc-watchdog.log -Tail 20
```

If you see `RECYCLE_OK` entries more than once per week, investigate which RFC is leaking NCo destinations (probably a recently-deployed controller).

## What this is NOT (anti-patterns)

For this specific failure mode (uniform 30s hang on every env via `/api/rfc/proxy`), do **NOT** spend time on:

| Hypothesis | Why it's wrong here |
|------------|---------------------|
| SAP DEV server down | `Test-NetConnection 192.168.144.174 -Port 3200` from .36 succeeds → SAP reachable |
| sap_abap user expired | If creds were bad, NCo would return `Name or password is incorrect` in 2-5s, not hang 30s |
| Azure Relay HCM dead | HHT DEV/QA path doesn't use Azure Relay — it goes via sap-api IIS direct over LAN |
| Cloudflare Tunnel broken | `/api/health` works; tunnel is fine |
| ACR / Container Registry | Server .36 runs IIS on bare metal, no container |
| Rotate SAP user to fix it | Pool restart is faster + safer + actually fixes it |

## Related paths (so you know what NOT to touch)

```
HHT DEV/QA traffic (this runbook covers this path):
  Zebra device (Dev/QA Cloud)
    -> hht-api.v2retail.net/{dev|qa}/api/hht/noacljsonrfcadaptor   (CF worker hht-proxy)
    -> sap-api.v2retail.net/api/rfc/proxy?env={dev|qa}             (IIS .36 V2RfcTestPool)  <-- pool wedge happens here
    -> NCo -> 192.168.144.174:3200 (DEV) or 192.168.144.179:3200 (QA)

HHT PROD traffic (this runbook does NOT cover this — different path entirely):
  Zebra device (V2 Cloud)
    -> hht-api.v2retail.net/prod/...
    -> v2-hht-api.azurewebsites.net/api/hht?env=prod               (Azure App Service)
    -> Azure Relay Hybrid Connection `java-mw-9080`
    -> 192.168.144.200:9080 Java middleware
    -> SAP PROD
```

PROD HHT is unaffected by V2RfcTestPool wedges. If PROD HHT is also broken, that's a separate incident.

## Triage checklist (copy-pasteable for incidents)

1. External smoke:
   ```bash
   curl -m 5 https://sap-api.v2retail.net/api/health
   # If 200 with "Status":"healthy" -> proceed
   ```
2. RFC probe:
   ```bash
   curl -m 30 -X POST "https://sap-api.v2retail.net/api/rfc/proxy?env=dev" \
     -H "X-RFC-Key: v2-rfc-proxy-2026" -H "Content-Type: application/json" \
     -d '{"bapiname":"STFC_CONNECTION","REQUTEXT":"PING"}'
   # If hangs 30s -> NCo wedge confirmed
   ```
3. Network from .36 (skip if step 2 hangs and watchdog already running — recycle will happen automatically within 10 min):
   ```powershell
   Test-NetConnection 192.168.144.174 -Port 3200
   ```
4. Recycle (manual override):
   ```powershell
   Import-Module WebAdministration
   Restart-WebAppPool -Name V2RfcTestPool
   ```
5. Re-test step 2. Should return data in <1s.

## See also

- Obsidian vault: `v2retail/V2RfcTestPool NCo Wedge 2026-05-26.md` — full incident retrospective
- Obsidian vault: `runbooks/Debug HHT.md` — broader HHT triage runbook
- Obsidian vault: `v2retail/HHT RFC Call Paths.md` — canonical routing for all 3 envs
- `docs/RFC-API-OVERVIEW.md` — overall rfc-api architecture
- `docs/PO-CREATOR-API.md` — example of a PROD-bound controller (separate code path, immune to this bug)
