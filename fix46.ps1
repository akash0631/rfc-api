
$ErrorActionPreference = "Continue"
$out = @()
$out += "=== $(hostname) @ $(Get-Date) ==="

# ── STEP 1: Check current state ──────────────────────────────────────────────
$rfcAppDlls = (Get-ChildItem C:\RfcApp -Filter "*.dll" -ErrorAction SilentlyContinue).Count
$out += "C:\RfcApp DLLs: $rfcAppDlls"

$workSrc = "C:\actions-runner\_work\rfc-api\rfc-api\bin\Release"
$workDlls = (Get-ChildItem $workSrc -Filter "*.dll" -ErrorAction SilentlyContinue).Count
$out += "Build output DLLs at work dir: $workDlls"

# ── STEP 2: Build if no output exists ────────────────────────────────────────
if ($workDlls -eq 0) {
    $out += "No build output — running MSBuild"
    
    # Write VS17 targets stub
    $targetsDir = "C:\Program Files (x86)\MSBuild\Microsoft\VisualStudio\v17.0\WebApplications"
    if (-not (Test-Path $targetsDir)) { New-Item -ItemType Directory $targetsDir -Force | Out-Null }
    $targetsFile = "$targetsDir\Microsoft.WebApplication.targets"
    $xml = '<?xml version="1.0" encoding="utf-8"?><Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Target Name="WebPublish" /><Target Name="Package" /></Project>'
    [System.IO.File]::WriteAllText($targetsFile, $xml, [System.Text.Encoding]::UTF8)
    $out += "VS17 targets written: $(Test-Path $targetsFile)"
    
    # Build
    $msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    $proj = "C:\actions-runner\_work\rfc-api\rfc-api\Vendor_SRM_Routing_Application.csproj"
    if (Test-Path $proj) {
        $proc = Start-Process $msbuild -ArgumentList "$proj /p:Configuration=Release /p:Platform=AnyCPU /p:OutputPath=bin\Release\ /m /nologo /verbosity:minimal /p:VisualStudioVersion=17.0" -WorkingDirectory "C:\actions-runner\_work\rfc-api\rfc-api" -Wait -PassThru -NoNewWindow
        $out += "MSBuild exit: $($proc.ExitCode)"
        $workDlls = (Get-ChildItem $workSrc -Filter "*.dll" -ErrorAction SilentlyContinue).Count
        $out += "DLLs after build: $workDlls"
    } else {
        $out += "Project not found at $proj"
        $out += "Work dir contents: $((Get-ChildItem 'C:\actions-runner\_work\rfc-api\rfc-api' -ErrorAction SilentlyContinue | Select -First 10 -ExpandProperty Name) -join ', ')"
    }
}

# ── STEP 3: Copy to C:\RfcApp ────────────────────────────────────────────────
if ($workDlls -gt 0 -and $rfcAppDlls -eq 0) {
    if (-not (Test-Path C:\RfcApp)) { New-Item -ItemType Directory C:\RfcApp -Force | Out-Null }
    Copy-Item "$workSrc\*" C:\RfcApp -Recurse -Force
    $wc = "C:\actions-runner\_work\rfc-api\rfc-api\Web.config"
    if (Test-Path $wc) { Copy-Item $wc C:\RfcApp\ -Force }
    $rfcAppDlls = (Get-ChildItem C:\RfcApp -Filter "*.dll" -ErrorAction SilentlyContinue).Count
    $out += "After copy: $rfcAppDlls DLLs in C:\RfcApp"
} else {
    $out += "Skipping copy (C:\RfcApp already has $rfcAppDlls DLLs)"
}

$vendor = (Get-ChildItem C:\RfcApp -Filter "Vendor*.dll" -ErrorAction SilentlyContinue | Select -First 2 -ExpandProperty Name) -join ", "
$sap    = (Get-ChildItem C:\RfcApp -ErrorAction SilentlyContinue | Where-Object {$_.Name -like "*sap*"} | Select -First 2 -ExpandProperty Name) -join ", "
$out += "Vendor DLLs: $vendor"
$out += "SAP DLLs: $sap"
$out += "Web.config: $(Test-Path C:\RfcApp\Web.config)"

# ── STEP 4: IIS ──────────────────────────────────────────────────────────────
Import-Module WebAdministration
Set-ItemProperty "IIS:\AppPools\RfcApiPool" -Name managedRuntimeVersion -Value "v4.0" -ErrorAction SilentlyContinue
Restart-WebAppPool "RfcApiPool" -ErrorAction SilentlyContinue
Start-Sleep 6
$siteState = (Get-Website "RfcApiApp" -ErrorAction SilentlyContinue).State
$out += "RfcApiApp state: $siteState"

# ── STEP 5: RFC Collector appsettings ────────────────────────────────────────
$cfg = "C:\RfcCollector\appsettings.json"
$json = Get-Content $cfg | ConvertFrom-Json
$json.IIS_HOST = "127.0.0.1"
$json.IIS_PORT = "8888"
$json | ConvertTo-Json -Depth 10 | Set-Content $cfg
$out += "Config: IIS_HOST=$($json.IIS_HOST) IIS_PORT=$($json.IIS_PORT)"

# ── STEP 6: Restart RFC Collector ────────────────────────────────────────────
Restart-Service RfcCollector -Force -ErrorAction SilentlyContinue
Start-Sleep 12
$svc = (Get-Service RfcCollector -ErrorAction SilentlyContinue).Status
$out += "RfcCollector: $svc"

# ── STEP 7: Test IIS ─────────────────────────────────────────────────────────
try {
    $h = Invoke-WebRequest "http://localhost:8888/api/ZAdvancePaymentRfc/Health" -UseBasicParsing -TimeoutSec 15
    $out += "IIS Health: HTTP $($h.StatusCode) $($h.Content)"
} catch {
    $out += "IIS Health ERROR: $($_.Exception.Message)"
}

# ── STEP 8: End-to-end fetch ──────────────────────────────────────────────────
$body = '{"rfc":"ZADVANCE_PAYMENT_RFC","params":{"I_COMPANY_CODE":"1000","I_POSTING_DATE_LOW":"20260301","I_POSTING_DATE_HIGH":"20260317"},"targetTable":"ET_ZADVANCE_PAYMENT","targetDb":"claudetestv2","maxRows":3}'
try {
    $r = Invoke-WebRequest "http://localhost:9090/fetch" -Method POST -Body $body -ContentType "application/json" -UseBasicParsing -TimeoutSec 60
    $out += "FETCH: HTTP $($r.StatusCode)"
    $out += $r.Content.Substring(0, [Math]::Min(800, $r.Content.Length))
} catch {
    $out += "FETCH ERROR: $($_.Exception.Message)"
}

$out | Out-File "fix_results.txt" -Encoding UTF8
Write-Host ($out -join "`n")
