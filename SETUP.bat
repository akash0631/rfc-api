@echo off
title V2 Retail RFC Pipeline - One-Time Setup
color 0B
echo.
echo  ==========================================
echo   V2 Retail - RFC Pipeline Worker Setup
echo  ==========================================
echo.
echo  This will deploy the RFC upload portal to Cloudflare.
echo  You will need to login to Cloudflare in your browser.
echo.
pause

:: Check node
node --version >nul 2>&1
if errorlevel 1 (
    echo  ERROR: Node.js not found. Install from nodejs.org then re-run this script.
    pause
    exit /b 1
)

echo  [1/5] Installing wrangler...
call npm install -g wrangler@latest --quiet

echo  [2/5] Logging in to Cloudflare (browser will open)...
call npx wrangler login
if errorlevel 1 (
    echo  Login failed. Please try again.
    pause
    exit /b 1
)

echo  [3/5] Deploying Worker...
call npx wrangler deploy
if errorlevel 1 (
    echo  Deployment failed. Check error above.
    pause
    exit /b 1
)

echo.
echo  [4/5] Setting secrets...
echo.
set /p ANTHROPIC_KEY="  Enter your Anthropic API key (sk-ant-...): "
echo %ANTHROPIC_KEY% | call npx wrangler secret put ANTHROPIC_API_KEY --name v2-rfc-pipeline

echo.
echo  [5/5] Setting GitHub token...
set /p GH_TOKEN="  Enter your GitHub token (ghp_...): "
echo %GH_TOKEN% | call npx wrangler secret put GITHUB_TOKEN --name v2-rfc-pipeline

echo.
echo  ==========================================
echo   DONE! Your RFC Portal is live at:
echo.
echo   https://v2-rfc-pipeline.workers.dev
echo.
echo   Swagger UI:
echo   https://v2-rfc-pipeline.workers.dev/swagger
echo  ==========================================
echo.
pause
