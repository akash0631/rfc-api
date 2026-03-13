# ================================================================
# V2 Retail RFC Pipeline - Full Test VM Setup
# Run as Administrator on: 192.168.151.46
# One-time setup. Safe to re-run - all steps are idempotent.
# ================================================================

$ErrorActionPreference = "Stop"
$REPO_URL   = "https://github.com/akash0631/rfc-api"
$REG_TOKEN  = "BU2HDPZYTDFAUKJ7IQFJ22TJWP5EM"
$IIS_PATH   = "C:\inetpub\wwwroot\Store Tracker"
$RUNNER_DIR = "C:\actions-runner"
$NUGET_DIR  = "C:\nuget"

function Log($msg)  { Write-Host "[$(Get-Date -Format HH:mm:ss)] $msg" -ForegroundColor Cyan }
function OK($msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function ERR($msg)  { Write-Host "  [!!] $msg" -ForegroundColor Red }
function SKIP($msg) { Write-Host "  [--] $msg (already done)" -ForegroundColor Gray }

Write-Host ""
Write-Host "  =================================================" -ForegroundColor Yellow
Write-Host "   V2 Retail RFC Pipeline - Test VM Setup" -ForegroundColor White
Write-Host "   Machine: $($env:COMPUTERNAME)" -ForegroundColor White
Write-Host "  =================================================" -ForegroundColor Yellow
Write-Host ""

# STEP 1: Install IIS
Log "STEP 1: Installing IIS..."
$iis = Get-WindowsFeature Web-Server
if ($iis.Installed) {
    SKIP "IIS already installed"
} else {
    Install-WindowsFeature -Name Web-Server,Web-Mgmt-Tools,Web-Asp-Net45,Web-Net-Ext45,Web-ISAPI-Ext,Web-ISAPI-Filter,Web-Default-Doc,Web-Static-Content -IncludeManagementTools | Out-Null
    OK "IIS installed"
}

# STEP 2: Create IIS app folder
Log "STEP 2: Creating IIS app folder..."
if (Test-Path "$IIS_PATH\bin") {
    SKIP "$IIS_PATH\bin already exists"
} else {
    New-Item -ItemType Directory -Force -Path "$IIS_PATH\bin" | Out-Null
    OK "Created: $IIS_PATH\bin"
}

# STEP 3: Create App Pool
Log "STEP 3: Creating IIS App Pool..."
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Test-Path "IIS:\AppPools\StoreTracker") {
    SKIP "App pool StoreTracker already exists"
} else {
    New-WebAppPool -Name "StoreTracker" | Out-Null
    Set-ItemProperty "IIS:\AppPools\StoreTracker" -Name "managedRuntimeVersion" -Value "v4.0"
    Set-ItemProperty "IIS:\AppPools\StoreTracker" -Name "managedPipelineMode" -Value "Integrated"
    OK "App pool StoreTracker created"
}

# STEP 4: Create IIS Website
Log "STEP 4: Creating IIS Website..."
if (Get-Website -Name "Store Tracker" -ErrorAction SilentlyContinue) {
    SKIP "Website Store Tracker already exists"
} else {
    # Remove default site if using port 80
    $def = Get-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
    if ($def) { Stop-Website -Name "Default Web Site"; Set-WebConfigurationProperty /system.applicationHost/sites/site[@name="Default Web Site"]/bindings/binding -Name bindingInformation -Value "*:8080:" }
    New-Website -Name "Store Tracker" -PhysicalPath $IIS_PATH -ApplicationPool "StoreTracker" -Port 80 -Force | Out-Null
    OK "Website Store Tracker created on port 80"
}

# STEP 5: Install NuGet
Log "STEP 5: Installing NuGet..."
if (Test-Path "$NUGET_DIR\nuget.exe") {
    SKIP "NuGet already installed"
} else {
    New-Item -ItemType Directory -Force -Path $NUGET_DIR | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "$NUGET_DIR\nuget.exe" -UseBasicParsing
    OK "NuGet installed"
}

# STEP 6: Install VS Build Tools
Log "STEP 6: Installing VS 2022 Build Tools (this takes 5-10 min)..."
$webTargets = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Microsoft\VisualStudio\v17.0\WebApplications\Microsoft.WebApplication.targets"
if (Test-Path $webTargets) {
    SKIP "VS Build Tools already installed"
} else {
    $installer = "$env:TEMP\vs_BuildTools.exe"
    Write-Host "  Downloading VS Build Tools..." -ForegroundColor Gray
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vs_BuildTools.exe" -OutFile $installer -UseBasicParsing
    Write-Host "  Installing (please wait ~10 min)..." -ForegroundColor Gray
    $proc = Start-Process -FilePath $installer -ArgumentList "--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.WebBuildTools" -Wait -PassThru
    if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
        OK "VS Build Tools installed"
    } else {
        ERR "VS Build Tools exit code: $($proc.ExitCode)"
    }
}

# STEP 7: Register GitHub Actions Runner
Log "STEP 7: Setting up GitHub Actions Runner..."
if (Test-Path "$RUNNER_DIR\run.cmd") {
    SKIP "Runner already downloaded"
} else {
    New-Item -ItemType Directory -Force -Path $RUNNER_DIR | Out-Null
    Write-Host "  Downloading runner (~80MB)..." -ForegroundColor Gray
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://github.com/actions/runner/releases/download/v2.323.0/actions-runner-win-x64-2.323.0.zip" -OutFile "$RUNNER_DIR\runner.zip" -UseBasicParsing
    Expand-Archive "$RUNNER_DIR\runner.zip" -DestinationPath $RUNNER_DIR -Force
    Remove-Item "$RUNNER_DIR\runner.zip"
    OK "Runner downloaded"
}

if (Test-Path "$RUNNER_DIR\.runner") {
    SKIP "Runner already configured"
} else {
    Push-Location $RUNNER_DIR
    & ".\config.cmd" --url $REPO_URL --token $REG_TOKEN --name TEST-VM-46 --runnergroup Default --labels "self-hosted,test-vm" --unattended --replace
    Pop-Location
    OK "Runner configured"
}

$svcName = "actions.runner.akash0631-rfc-api.TEST-VM-46"
$svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") {
    SKIP "Runner service already running"
} else {
    Push-Location $RUNNER_DIR
    & ".\svc.cmd" install
    & ".\svc.cmd" start
    Pop-Location
    OK "Runner service started"
}

# STEP 8: Verify
Log "STEP 8: Verifying setup..."
Write-Host ""

$site = Get-Website -Name "Store Tracker" -ErrorAction SilentlyContinue
if ($site) { OK "IIS site: Store Tracker (port 80, state: $($site.State))" } else { ERR "IIS site NOT found" }

if (Test-Path "$IIS_PATH\bin") { OK "IIS folder: $IIS_PATH\bin" } else { ERR "IIS folder MISSING" }

if (Test-Path "$NUGET_DIR\nuget.exe") { OK "NuGet: ready" } else { ERR "NuGet: MISSING" }

if (Test-Path $webTargets) { OK "VS Build Tools: WebApplication.targets found" } else { ERR "VS Build Tools: targets MISSING" }

$svc2 = Get-Service -Name $svcName -ErrorAction SilentlyContinue
if ($svc2 -and $svc2.Status -eq "Running") { OK "GitHub Actions runner: RUNNING" } else { ERR "GitHub Actions runner: NOT running" }

Write-Host "  Testing SQL 192.168.151.28..." -ForegroundColor Gray
try {
    $conn = New-Object System.Data.SqlClient.SqlConnection("Server=192.168.151.28,1433;Database=DataV2;User Id=sa;Password=vrl@55555;Connect Timeout=5;")
    $conn.Open(); $conn.Close()
    OK "SQL DataV2 @ .28: REACHABLE"
} catch { ERR "SQL DataV2 @ .28: FAILED - $_" }

Write-Host "  Testing SAP Dev 192.168.144.174..." -ForegroundColor Gray
if (Test-Connection -ComputerName "192.168.144.174" -Count 1 -Quiet) {
    OK "SAP Dev @ .174: REACHABLE"
} else {
    ERR "SAP Dev @ .174: NOT reachable"
}

Write-Host ""
Write-Host "  =================================================" -ForegroundColor Green
Write-Host "   SETUP COMPLETE" -ForegroundColor Green
Write-Host "   Next: go to GitHub Actions and run" -ForegroundColor White
Write-Host "   Deploy to Test VM (192.168.151.46)" -ForegroundColor White
Write-Host "  =================================================" -ForegroundColor Green
Write-Host ""
