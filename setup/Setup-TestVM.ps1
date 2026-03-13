# ================================================================
# V2 Retail RFC Pipeline - Full Test VM Setup
# Run as Administrator on: 192.168.151.46
# One-time setup. Safe to re-run — all steps are idempotent.
# ================================================================

$ErrorActionPreference = "Stop"
$HOST_NAME  = $env:COMPUTERNAME
$REPO_URL   = "https://github.com/akash0631/rfc-api"
$REG_TOKEN  = "BU2HDP3OEXOKBKTW6SHGJW3JWP3OC"
$IIS_PATH   = "C:\inetpub\wwwroot\Store Tracker"
$RUNNER_DIR = "C:\actions-runner"
$NUGET_DIR  = "C:\nuget"
$LOG        = "C:\setup-rfc-testvm.log"

function Log($msg) {
    $ts = Get-Date -Format "HH:mm:ss"
    $line = "[$ts] $msg"
    Write-Host $line -ForegroundColor Cyan
    Add-Content $LOG $line
}

function OK($msg)  { Write-Host "  OK: $msg" -ForegroundColor Green;  Add-Content $LOG "  OK: $msg" }
function ERR($msg) { Write-Host "  !! $msg" -ForegroundColor Red;    Add-Content $LOG "  !! $msg" }
function SKIP($msg){ Write-Host "  -- $msg (skipped)" -ForegroundColor Gray; Add-Content $LOG "  -- SKIP: $msg" }

Start-Transcript -Path $LOG -Append -NoClobber | Out-Null

Write-Host ""
Write-Host "  =================================================" -ForegroundColor Yellow
Write-Host "   V2 Retail RFC Pipeline - Test VM Setup" -ForegroundColor White
Write-Host "   Machine: $HOST_NAME" -ForegroundColor White
Write-Host "  =================================================" -ForegroundColor Yellow
Write-Host ""

# ── STEP 1: Install IIS ──────────────────────────────────────
Log "STEP 1: Installing IIS..."
$iis = Get-WindowsFeature Web-Server
if ($iis.Installed) {
    SKIP "IIS already installed"
} else {
    Install-WindowsFeature -Name Web-Server, Web-Mgmt-Tools, Web-Asp-Net45, Web-Net-Ext45, Web-ISAPI-Ext, Web-ISAPI-Filter, Web-Default-Doc, Web-Static-Content -IncludeManagementTools | Out-Null
    OK "IIS installed"
}

# ── STEP 2: Create IIS site folder ───────────────────────────
Log "STEP 2: Creating IIS app folder..."
if (Test-Path "$IIS_PATH\bin") {
    SKIP "Folder already exists: $IIS_PATH\bin"
} else {
    New-Item -ItemType Directory -Force -Path "$IIS_PATH\bin" | Out-Null
    OK "Created: $IIS_PATH\bin"
}

# ── STEP 3: Create App Pool ───────────────────────────────────
Log "STEP 3: Creating IIS App Pool..."
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Test-Path "IIS:\AppPools\StoreTracker") {
    SKIP "App pool 'StoreTracker' already exists"
} else {
    New-WebAppPool -Name "StoreTracker" | Out-Null
    Set-ItemProperty "IIS:\AppPools\StoreTracker" -Name "managedRuntimeVersion" -Value "v4.0"
    Set-ItemProperty "IIS:\AppPools\StoreTracker" -Name "managedPipelineMode"   -Value "Integrated"
    Set-ItemProperty "IIS:\AppPools\StoreTracker" -Name "startMode"             -Value "AlwaysRunning"
    OK "App pool 'StoreTracker' created (.NET 4.0, Integrated)"
}

# ── STEP 4: Create IIS Website ───────────────────────────────
Log "STEP 4: Creating IIS Website..."
if (Get-Website -Name "Store Tracker" -ErrorAction SilentlyContinue) {
    SKIP "Website 'Store Tracker' already exists"
} else {
    New-Website -Name "Store Tracker" -PhysicalPath $IIS_PATH -ApplicationPool "StoreTracker" -Port 80 -Force | Out-Null
    OK "Website 'Store Tracker' created on port 80"
}

# ── STEP 5: Install NuGet ─────────────────────────────────────
Log "STEP 5: Installing NuGet..."
if (Test-Path "$NUGET_DIR\nuget.exe") {
    SKIP "NuGet already installed"
} else {
    New-Item -ItemType Directory -Force -Path $NUGET_DIR | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "$NUGET_DIR\nuget.exe"
    OK "NuGet installed at $NUGET_DIR\nuget.exe"
}

# ── STEP 6: Install VS Build Tools ───────────────────────────
Log "STEP 6: Installing VS 2022 Build Tools (ASP.NET workload)..."
$vsWhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
$webTargets = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Microsoft\VisualStudio\v17.0\WebApplications\Microsoft.WebApplication.targets"
if (Test-Path $webTargets) {
    SKIP "VS Build Tools + WebApplication targets already installed"
} else {
    $installer = "$env:TEMP\vs_BuildTools.exe"
    Write-Host "  Downloading VS Build Tools (~5MB bootstrapper)..." -ForegroundColor Gray
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vs_BuildTools.exe" -OutFile $installer
    Write-Host "  Installing (this takes 5-10 min, please wait)..." -ForegroundColor Gray
    $args = "--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.WebBuildTools"
    $proc = Start-Process -FilePath $installer -ArgumentList $args -Wait -PassThru
    if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
        OK "VS Build Tools installed (exit code: $($proc.ExitCode))"
    } else {
        ERR "VS Build Tools installer exited with code: $($proc.ExitCode)"
    }
}

# ── STEP 7: Register GitHub Actions Runner ────────────────────
Log "STEP 7: Registering GitHub Actions Runner..."
if (Test-Path "$RUNNER_DIR\run.cmd") {
    SKIP "Runner already downloaded"
} else {
    New-Item -ItemType Directory -Force -Path $RUNNER_DIR | Out-Null
    $runnerUrl = "https://github.com/actions/runner/releases/download/v2.323.0/actions-runner-win-x64-2.323.0.zip"
    Write-Host "  Downloading runner (~80MB)..." -ForegroundColor Gray
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $runnerUrl -OutFile "$RUNNER_DIR\runner.zip"
    Expand-Archive "$RUNNER_DIR\runner.zip" -DestinationPath $RUNNER_DIR -Force
    Remove-Item "$RUNNER_DIR\runner.zip"
    OK "Runner downloaded and extracted"
}

if (Test-Path "$RUNNER_DIR\.runner") {
    SKIP "Runner already configured"
} else {
    Push-Location $RUNNER_DIR
    $configArgs = "--url $REPO_URL --token $REG_TOKEN --name TEST-VM-46 --runnergroup Default --labels self-hosted,test-vm --unattended --replace"
    & "$RUNNER_DIR\config.cmd" $configArgs.Split(" ")
    Pop-Location
    OK "Runner configured with label 'test-vm'"
}

$svcName = "actions.runner.akash0631-rfc-api.TEST-VM-46"
if ((Get-Service -Name $svcName -ErrorAction SilentlyContinue).Status -eq "Running") {
    SKIP "Runner service already running"
} else {
    Push-Location $RUNNER_DIR
    & "$RUNNER_DIR\svc.cmd" install
    & "$RUNNER_DIR\svc.cmd" start
    Pop-Location
    OK "Runner service installed and started"
}

# ── STEP 8: Verify everything ─────────────────────────────────
Log "STEP 8: Verification..."
Write-Host ""
Write-Host "  ── Results ──────────────────────────────────────" -ForegroundColor Yellow

# IIS
$site = Get-Website -Name "Store Tracker" -ErrorAction SilentlyContinue
if ($site) { OK "IIS site 'Store Tracker' → State: $($site.State), Port: 80" }
else        { ERR "IIS site NOT found" }

# Bin folder
if (Test-Path "$IIS_PATH\bin") { OK "IIS bin folder exists: $IIS_PATH\bin" }
else                            { ERR "IIS bin folder MISSING" }

# NuGet
if (Test-Path "$NUGET_DIR\nuget.exe") { OK "NuGet: $NUGET_DIR\nuget.exe" }
else                                   { ERR "NuGet MISSING" }

# MSBuild
$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if (Test-Path $msbuild) { OK "MSBuild: $msbuild" }
else                     { ERR "MSBuild MISSING" }

# WebApplication targets
if (Test-Path $webTargets) { OK "WebApplication.targets found" }
else                        { ERR "WebApplication.targets MISSING — VS Build Tools may still be installing" }

# Runner service
$svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") { OK "GitHub Actions runner: RUNNING" }
else                                       { ERR "GitHub Actions runner: NOT running" }

# SQL reachability
Write-Host "  Testing SQL connection to 192.168.151.28..." -ForegroundColor Gray
try {
    $conn = New-Object System.Data.SqlClient.SqlConnection("Server=192.168.151.28,1433;Database=DataV2;User Id=sa;Password=vrl@55555;Connect Timeout=5;")
    $conn.Open()
    $conn.Close()
    OK "SQL DataV2 @ 192.168.151.28: REACHABLE"
} catch {
    ERR "SQL DataV2 @ 192.168.151.28: FAILED — $_"
}

# SAP Dev reachability (ping only)
Write-Host "  Testing SAP Dev reachability at 192.168.144.174..." -ForegroundColor Gray
if (Test-Connection -ComputerName "192.168.144.174" -Count 1 -Quiet) {
    OK "SAP Dev @ 192.168.144.174: REACHABLE"
} else {
    ERR "SAP Dev @ 192.168.144.174: NOT reachable (check network/VPN)"
}

Write-Host ""
Write-Host "  =================================================" -ForegroundColor Green
Write-Host "   SETUP COMPLETE — log saved to $LOG" -ForegroundColor Green
Write-Host "   Next: push a commit to GitHub to trigger deploy" -ForegroundColor White
Write-Host "  =================================================" -ForegroundColor Green
Write-Host ""

Stop-Transcript | Out-Null
