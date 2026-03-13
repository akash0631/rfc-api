#Requires -RunAsAdministrator
<#
.SYNOPSIS
    One-shot setup for V2 Retail Data Lake Sync on V2DC-ADDVERB.
    Run once as Administrator. Does 3 things:
      1. Installs & registers the GitHub Actions self-hosted runner
         (so future controller pushes auto-deploy to IIS)
      2. Creates Windows Scheduled Task: Nightly Sync at 02:00 IST
      3. Creates Windows Scheduled Task: Poll trigger queue every 1 min

.USAGE
    Right-click PowerShell → "Run as administrator"
    .\Setup-DataLakeSync.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  V2 Retail — Data Lake Sync Setup" -ForegroundColor Cyan
Write-Host "  V2DC-ADDVERB · $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ── CONFIG ─────────────────────────────────────────────────────────────────────
$RunnerDir     = "C:\actions-runner"
$RunnerVersion = "2.323.0"
$RunnerUrl     = "https://github.com/actions/runner/releases/download/v$RunnerVersion/actions-runner-win-x64-$RunnerVersion.zip"
$RepoUrl       = "https://github.com/akash0631/rfc-api"
$RunnerToken   = "BU2HDP7TCUWCZESV6JOBT3TJWPHQU"    # valid for 1hr from generation
$ApiBase       = "http://localhost/api"
$TaskUser      = "SYSTEM"

# ──────────────────────────────────────────────────────────────────────────────
# STEP 1 — GitHub Actions Self-Hosted Runner
# ──────────────────────────────────────────────────────────────────────────────
Write-Host "[1/3] Installing GitHub Actions Runner..." -ForegroundColor Yellow

if (Test-Path "$RunnerDir\run.cmd") {
    Write-Host "      Runner already installed at $RunnerDir — skipping download" -ForegroundColor DarkGray
} else {
    if (-not (Test-Path $RunnerDir)) { New-Item -ItemType Directory -Path $RunnerDir | Out-Null }

    Write-Host "      Downloading runner v$RunnerVersion..."
    $zip = "$env:TEMP\actions-runner.zip"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $RunnerUrl -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $RunnerDir -Force
    Remove-Item $zip

    Write-Host "      Configuring runner for akash0631/rfc-api..."
    Push-Location $RunnerDir
    .\config.cmd --url $RepoUrl `
                 --token $RunnerToken `
                 --name "V2DC-ADDVERB" `
                 --labels "self-hosted,windows,iis" `
                 --runnergroup "Default" `
                 --unattended `
                 --replace 2>&1
    Pop-Location
}

# Install as Windows Service so it starts automatically on reboot
Push-Location $RunnerDir
if (-not (Get-Service "actions.runner.*" -ErrorAction SilentlyContinue)) {
    Write-Host "      Installing runner as Windows Service..."
    .\svc.cmd install 2>&1
    .\svc.cmd start   2>&1
    Write-Host "      ✅ Runner service started" -ForegroundColor Green
} else {
    $svc = Get-Service "actions.runner.*"
    Write-Host "      Runner service already exists: $($svc.Name) [$($svc.Status)]" -ForegroundColor DarkGray
    if ($svc.Status -ne "Running") { $svc.Start() }
}
Pop-Location

# ──────────────────────────────────────────────────────────────────────────────
# STEP 2 — Scheduled Task: Nightly Sync (02:00 IST = 20:30 UTC)
# ──────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[2/3] Creating Scheduled Task: Nightly Sync at 02:00 IST..." -ForegroundColor Yellow

$TaskName1 = "V2_DataLake_NightlySync"

$Action1 = New-ScheduledTaskAction `
    -Execute "curl.exe" `
    -Argument "-s -X POST $ApiBase/Sync/run-all -o C:\inetpub\logs\DataLakeSync\nightly_sync.log"

$Trigger1 = New-ScheduledTaskTrigger -Daily -At "20:30"   # 20:30 UTC = 02:00 IST

$Settings1 = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -RestartCount 2 `
    -RestartInterval (New-TimeSpan -Minutes 5) `
    -StartWhenAvailable `
    -RunOnlyIfNetworkAvailable

# Ensure log dir exists
$logDir = "C:\inetpub\logs\DataLakeSync"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

if (Get-ScheduledTask -TaskName $TaskName1 -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName1 -Confirm:$false
}
Register-ScheduledTask `
    -TaskName $TaskName1 `
    -Action $Action1 `
    -Trigger $Trigger1 `
    -Settings $Settings1 `
    -RunLevel Highest `
    -User $TaskUser `
    -Force | Out-Null

Write-Host "      ✅ Task '$TaskName1' created — runs daily 20:30 UTC (02:00 IST)" -ForegroundColor Green

# ──────────────────────────────────────────────────────────────────────────────
# STEP 3 — Scheduled Task: Poll for manual triggers (every 1 min)
# ──────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[3/3] Creating Scheduled Task: Poll every 1 min..." -ForegroundColor Yellow

$TaskName2 = "V2_DataLake_PollTriggers"

$Action2 = New-ScheduledTaskAction `
    -Execute "curl.exe" `
    -Argument "-s $ApiBase/Sync/poll -o C:\inetpub\logs\DataLakeSync\poll.log"

# Trigger: run at startup, repeat every 1 minute indefinitely
$Trigger2 = New-ScheduledTaskTrigger -AtStartup
$Trigger2.Repetition = (New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 1) -Once -At (Get-Date)).Repetition

$Settings2 = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 2) `
    -MultipleInstances IgnoreNew `
    -StartWhenAvailable `
    -RunOnlyIfNetworkAvailable

if (Get-ScheduledTask -TaskName $TaskName2 -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName2 -Confirm:$false
}
Register-ScheduledTask `
    -TaskName $TaskName2 `
    -Action $Action2 `
    -Trigger $Trigger2 `
    -Settings $Settings2 `
    -RunLevel Highest `
    -User $TaskUser `
    -Force | Out-Null

# Start it immediately
Start-ScheduledTask -TaskName $TaskName2
Write-Host "      ✅ Task '$TaskName2' created and started (polls every 1 min)" -ForegroundColor Green

# ──────────────────────────────────────────────────────────────────────────────
# DONE
# ──────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "  ✅  Setup complete!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "  GitHub Runner : V2DC-ADDVERB (self-hosted, windows, iis)" -ForegroundColor White
Write-Host "                  Future pushes to Controllers/ auto-deploy to IIS" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Nightly Sync  : Daily 20:30 UTC → POST $ApiBase/Sync/run-all" -ForegroundColor White
Write-Host "  Poll Triggers : Every 1 min    → GET  $ApiBase/Sync/poll" -ForegroundColor White
Write-Host "  Log dir       : C:\inetpub\logs\DataLakeSync\" -ForegroundColor White
Write-Host ""
Write-Host "  Dashboard     : https://v2-rfc-pipeline.akash-bab.workers.dev/sync" -ForegroundColor Cyan
Write-Host ""

# Verify tasks
Write-Host "  Scheduled Task Status:" -ForegroundColor Yellow
Get-ScheduledTask -TaskName $TaskName1, $TaskName2 |
    Format-Table TaskName, State -AutoSize
