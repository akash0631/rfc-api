# ============================================================
# V2DC-ADDVERB: GitHub Actions Self-Hosted Runner Setup
# Run this ONCE on V2DC-ADDVERB as Administrator
# ============================================================

$ErrorActionPreference = "Stop"
$RUNNER_DIR = "C:\actions-runner"
$REPO_URL   = "https://github.com/akash0631/rfc-api"

Write-Host "=== V2 Retail RFC Pipeline: Self-Hosted Runner Setup ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Create runner directory
if (-not (Test-Path $RUNNER_DIR)) {
    New-Item -ItemType Directory -Path $RUNNER_DIR | Out-Null
    Write-Host "[1/5] Created $RUNNER_DIR" -ForegroundColor Green
} else {
    Write-Host "[1/5] $RUNNER_DIR already exists" -ForegroundColor Yellow
}

# Step 2: Download latest runner
Write-Host "[2/5] Downloading GitHub Actions runner..." -ForegroundColor Cyan
$RUNNER_VERSION = "2.321.0"
$RUNNER_ZIP = "$RUNNER_DIR\runner.zip"
$RUNNER_URL = "https://github.com/actions/runner/releases/download/v$RUNNER_VERSION/actions-runner-win-x64-$RUNNER_VERSION.zip"
Invoke-WebRequest -Uri $RUNNER_URL -OutFile $RUNNER_ZIP
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($RUNNER_ZIP, $RUNNER_DIR)
Remove-Item $RUNNER_ZIP
Write-Host "[2/5] Runner downloaded and extracted" -ForegroundColor Green

# Step 3: Get registration token (needs to be done via browser)
Write-Host ""
Write-Host "[3/5] MANUAL STEP REQUIRED:" -ForegroundColor Yellow
Write-Host "  1. Open: https://github.com/akash0631/rfc-api/settings/actions/runners/new"
Write-Host "  2. Copy the token shown under 'Configure' (looks like: AXXXXXXXXXX...)"
Write-Host ""
$TOKEN = Read-Host "Paste the runner registration token here"

# Step 4: Configure the runner
Write-Host "[4/5] Configuring runner..." -ForegroundColor Cyan
Set-Location $RUNNER_DIR
.\config.cmd `
    --url $REPO_URL `
    --token $TOKEN `
    --name "V2DC-ADDVERB" `
    --labels "self-hosted,windows,iis,sap-vpn" `
    --work "_work" `
    --runasservice `
    --windowslogonaccount "NT AUTHORITY\SYSTEM" `
    --unattended

Write-Host "[4/5] Runner configured" -ForegroundColor Green

# Step 5: Install and start as Windows service
Write-Host "[5/5] Installing as Windows service..." -ForegroundColor Cyan
.\svc.cmd install
.\svc.cmd start
Write-Host "[5/5] Service started" -ForegroundColor Green

Write-Host ""
Write-Host "=== SETUP COMPLETE ===" -ForegroundColor Green
Write-Host "Runner 'V2DC-ADDVERB' is now registered and running as a service."
Write-Host "Any push to Controllers/ will now auto-build and deploy to IIS."
Write-Host ""
Write-Host "Verify at: https://github.com/akash0631/rfc-api/settings/actions/runners"
