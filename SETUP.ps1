Write-Host ""
Write-Host "  ==========================================" -ForegroundColor Cyan
Write-Host "   V2 Retail - RFC Pipeline Setup" -ForegroundColor White
Write-Host "  ==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Deploying RFC upload portal to Cloudflare Workers." -ForegroundColor Gray
Write-Host "  A browser window will open for Cloudflare login." -ForegroundColor Gray
Write-Host ""
Read-Host "  Press Enter to start"

# Install wrangler
Write-Host "`n  [1/5] Installing wrangler..." -ForegroundColor Yellow
npm install -g wrangler@latest --quiet

# Login
Write-Host "`n  [2/5] Cloudflare login (browser will open)..." -ForegroundColor Yellow
npx wrangler login
if ($LASTEXITCODE -ne 0) { Write-Host "  Login failed." -ForegroundColor Red; exit 1 }

# Deploy
Write-Host "`n  [3/5] Deploying Worker..." -ForegroundColor Yellow
Set-Location (Split-Path -Parent $PSCommandPath)
npx wrangler deploy
if ($LASTEXITCODE -ne 0) { Write-Host "  Deploy failed." -ForegroundColor Red; exit 1 }

# Set Anthropic key
Write-Host "`n  [4/5] Anthropic API key..." -ForegroundColor Yellow
$apiKey = Read-Host "  Enter your Anthropic API key (sk-ant-...)"
echo $apiKey | npx wrangler secret put ANTHROPIC_API_KEY --name v2-rfc-pipeline

# Set GitHub token (pre-filled)
Write-Host "`n  [5/5] Setting GitHub token (pre-configured)..." -ForegroundColor Yellow
$GH_TOKEN = Read-Host "  Enter your GitHub token (ghp_...)"
$GH_TOKEN | npx wrangler secret put GITHUB_TOKEN --name v2-rfc-pipeline

Write-Host ""
Write-Host "  ==========================================" -ForegroundColor Green
Write-Host "  SETUP COMPLETE" -ForegroundColor Green
Write-Host ""
Write-Host "  Your RFC Portal URL:" -ForegroundColor White
Write-Host "  https://v2-rfc-pipeline.workers.dev" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Swagger UI:" -ForegroundColor White
Write-Host "  https://v2-rfc-pipeline.workers.dev/swagger" -ForegroundColor Cyan
Write-Host "  ==========================================" -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to close"
