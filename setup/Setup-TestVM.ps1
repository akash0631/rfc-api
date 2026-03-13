# V2 Retail RFC Pipeline - Test VM Setup
# Run as Administrator. No file I/O - screen output only.

$ErrorActionPreference = "Stop"
$REPO_URL   = "https://github.com/akash0631/rfc-api"
$REG_TOKEN  = "BU2HDP3V2MQ3FUBM5JIX3HTJWP5WS"
$IIS_PATH   = "C:\inetpub\wwwroot\Store Tracker"
$RUNNER_DIR = "C:\actions-runner"
$NUGET_DIR  = "C:\nuget"

function Log($m)  { Write-Host "[$(Get-Date -Format HH:mm:ss)] $m" -ForegroundColor Cyan }
function OK($m)   { Write-Host "  [OK] $m" -ForegroundColor Green }
function ERR($m)  { Write-Host "  [!!] $m" -ForegroundColor Red }
function SKIP($m) { Write-Host "  [--] $m" -ForegroundColor Gray }

Write-Host ""
Write-Host "  V2 Retail RFC Pipeline - Test VM Setup" -ForegroundColor Yellow
Write-Host "  Machine: $($env:COMPUTERNAME)" -ForegroundColor White
Write-Host ""

# STEP 1: IIS
Log "STEP 1: Installing IIS..."
if ((Get-WindowsFeature Web-Server).Installed) {
    SKIP "IIS already installed"
} else {
    Install-WindowsFeature -Name Web-Server,Web-Mgmt-Tools,Web-Asp-Net45,Web-Net-Ext45,Web-ISAPI-Ext,Web-ISAPI-Filter -IncludeManagementTools | Out-Null
    OK "IIS installed"
}

# STEP 2: IIS folder
Log "STEP 2: IIS app folder..."
if (Test-Path "$IIS_PATH\bin") { SKIP "Already exists" }
else { New-Item -ItemType Directory -Force -Path "$IIS_PATH\bin" | Out-Null; OK "Created $IIS_PATH\bin" }

# STEP 3: App Pool
Log "STEP 3: App Pool..."
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Test-Path "IIS:\AppPools\StoreTracker") { SKIP "StoreTracker pool exists" }
else {
    New-WebAppPool -Name "StoreTracker" | Out-Null
    Set-ItemProperty "IIS:\AppPools\StoreTracker" -Name managedRuntimeVersion -Value "v4.0"
    Set-ItemProperty "IIS:\AppPools\StoreTracker" -Name managedPipelineMode -Value "Integrated"
    OK "StoreTracker pool created"
}

# STEP 4: Website
Log "STEP 4: IIS Website..."
if (Get-Website -Name "Store Tracker" -ErrorAction SilentlyContinue) { SKIP "Site exists" }
else {
    $def = Get-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
    if ($def) { Stop-Website -Name "Default Web Site" }
    New-Website -Name "Store Tracker" -PhysicalPath $IIS_PATH -ApplicationPool "StoreTracker" -Port 80 -Force | Out-Null
    OK "Site created on port 80"
}

# STEP 5: NuGet
Log "STEP 5: NuGet..."
if (Test-Path "$NUGET_DIR\nuget.exe") { SKIP "Already installed" }
else {
    New-Item -ItemType Directory -Force -Path $NUGET_DIR | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "$NUGET_DIR\nuget.exe" -UseBasicParsing
    OK "NuGet installed"
}

# STEP 6: VS Build Tools
Log "STEP 6: VS 2022 Build Tools (wait ~10 min)..."
$webTargets = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Microsoft\VisualStudio\v17.0\WebApplications\Microsoft.WebApplication.targets"
if (Test-Path $webTargets) { SKIP "Already installed" }
else {
    $inst = "$env:TEMP\vs_bt.exe"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vs_BuildTools.exe" -OutFile $inst -UseBasicParsing
    $p = Start-Process $inst "--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.WebBuildTools" -Wait -PassThru
    if ($p.ExitCode -in 0,3010) { OK "VS Build Tools installed" }
    else { ERR "Exit code: $($p.ExitCode)" }
}

# STEP 7: Runner
Log "STEP 7: GitHub Actions Runner..."
if (-not (Test-Path "$RUNNER_DIR\run.cmd")) {
    New-Item -ItemType Directory -Force -Path $RUNNER_DIR | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://github.com/actions/runner/releases/download/v2.323.0/actions-runner-win-x64-2.323.0.zip" -OutFile "$RUNNER_DIR\runner.zip" -UseBasicParsing
    Expand-Archive "$RUNNER_DIR\runner.zip" -DestinationPath $RUNNER_DIR -Force
    Remove-Item "$RUNNER_DIR\runner.zip"
    OK "Runner downloaded"
} else { SKIP "Runner already downloaded" }

if (-not (Test-Path "$RUNNER_DIR\.runner")) {
    Push-Location $RUNNER_DIR
    .\config.cmd --url $REPO_URL --token $REG_TOKEN --name TEST-VM-46 --labels "self-hosted,test-vm" --unattended --replace
    Pop-Location
    OK "Runner configured"
} else { SKIP "Runner already configured" }

$svcName = "actions.runner.akash0631-rfc-api.TEST-VM-46"
$svc = Get-Service $svcName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") { SKIP "Runner service running" }
else {
    Push-Location $RUNNER_DIR
    .\svc.cmd install
    .\svc.cmd start
    Pop-Location
    OK "Runner service started"
}

# STEP 8: Verify
Log "STEP 8: Verification..."
Write-Host ""

if (Get-Website "Store Tracker" -ErrorAction SilentlyContinue) { OK "IIS site: Store Tracker" } else { ERR "IIS site missing" }
if (Test-Path "$IIS_PATH\bin") { OK "IIS folder: OK" } else { ERR "IIS folder missing" }
if (Test-Path "$NUGET_DIR\nuget.exe") { OK "NuGet: OK" } else { ERR "NuGet missing" }
if (Test-Path $webTargets) { OK "VS Build Tools: OK" } else { ERR "VS Build Tools missing" }

$svc2 = Get-Service $svcName -ErrorAction SilentlyContinue
if ($svc2 -and $svc2.Status -eq "Running") { OK "GitHub runner: RUNNING" } else { ERR "GitHub runner: NOT running" }

try {
    $c = New-Object System.Data.SqlClient.SqlConnection("Server=192.168.151.28,1433;Database=DataV2;User Id=sa;Password=vrl@55555;Connect Timeout=5;")
    $c.Open(); $c.Close(); OK "SQL .28: REACHABLE"
} catch { ERR "SQL .28: FAILED" }

if (Test-Connection "192.168.144.174" -Count 1 -Quiet) { OK "SAP Dev .174: REACHABLE" }
else { ERR "SAP Dev .174: not reachable" }

Write-Host ""
Write-Host "  DONE. Go to GitHub Actions -> Deploy to Test VM" -ForegroundColor Green
Write-Host ""
