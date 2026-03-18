###############################################################
#  START-SERVER.ps1
#  Deploys the RFC API to http://192.168.151.46:8888 via GitHub Actions
#  Usage:  Right-click > Run with PowerShell   OR   .\START-SERVER.ps1
###############################################################

$TOKEN   = "YOUR_GITHUB_TOKEN_HERE"   # Replace with your GitHub Personal Access Token
$REPO    = "akash0631/rfc-api"
$WF_ID   = "245608998"
$SERVER  = "http://192.168.151.46:8888"
$HEADERS = @{ "Authorization" = "Bearer $TOKEN"; "User-Agent" = "RFC-Deploy"; "Content-Type" = "application/json" }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RFC API  -  Deploy & Start Server    " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Trigger workflow ──────────────────────────────────
Write-Host "[1/3] Triggering GitHub Actions deploy..." -ForegroundColor Yellow
$body = @{ ref = "master"; inputs = @{ rfc_name = "ZAdvancePaymentRfc" } } | ConvertTo-Json
try {
    Invoke-RestMethod -Uri "https://api.github.com/repos/$REPO/actions/workflows/$WF_ID/dispatches" `
        -Method Post -Headers $HEADERS -Body $body | Out-Null
    Write-Host "      Deploy triggered OK" -ForegroundColor Green
} catch {
    Write-Host "      ERROR triggering deploy: $_" -ForegroundColor Red
    Read-Host "Press Enter to exit"; exit 1
}

# Wait a few seconds for GitHub to register the run
Start-Sleep -Seconds 5

# ── Step 2: Get the new run ID ────────────────────────────────
Write-Host "[2/3] Waiting for deploy to complete (this takes ~2 minutes)..." -ForegroundColor Yellow
$runId = $null
for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep -Seconds 3
    try {
        $runs = Invoke-RestMethod "https://api.github.com/repos/$REPO/actions/workflows/$WF_ID/runs?per_page=1" -Headers $HEADERS
        $run = $runs.workflow_runs[0]
        if ($run.status -eq "in_progress" -or $run.status -eq "queued") {
            $runId = $run.id
            Write-Host "      Run $runId started (sha: $($run.head_sha.Substring(0,7)))" -ForegroundColor Gray
            break
        }
    } catch { }
}

if (-not $runId) {
    Write-Host "      Could not get run ID - checking server directly..." -ForegroundColor Red
}

# ── Poll until completed ──────────────────────────────────────
$dots = 0
$maxWait = 300  # 5 minutes max
$waited  = 0
$conclusion = $null

while ($waited -lt $maxWait) {
    Start-Sleep -Seconds 8
    $waited += 8
    Write-Host -NoNewline "." -ForegroundColor Gray
    $dots++
    if ($dots % 30 -eq 0) { Write-Host "" }

    try {
        $status = Invoke-RestMethod "https://api.github.com/repos/$REPO/actions/runs/$runId" -Headers $HEADERS
        if ($status.status -eq "completed") {
            $conclusion = $status.conclusion
            break
        }
    } catch { }
}

Write-Host ""

# ── Step 3: Show result ───────────────────────────────────────
Write-Host "[3/3] Result: " -ForegroundColor Yellow -NoNewline
if ($conclusion -eq "success") {
    Write-Host "SUCCESS" -ForegroundColor Green
} elseif ($conclusion) {
    Write-Host "$conclusion (site may still be up with old code)" -ForegroundColor DarkYellow
} else {
    Write-Host "timeout / unknown" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Server URLs:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Main site  :  $SERVER/"                   -ForegroundColor White
Write-Host "  Swagger UI :  $SERVER/swagger/ui/index"   -ForegroundColor White
Write-Host "  ZAdvance   :  $SERVER/api/ZAdvancePaymentRfc" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Opening Swagger in browser..." -ForegroundColor Yellow
Start-Process "$SERVER/swagger/ui/index"

Write-Host ""
Read-Host "Press Enter to exit"
