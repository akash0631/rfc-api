
$ErrorActionPreference = "Continue"
$out = @()
$out += "=== $(hostname) @ $(Get-Date) ==="

# 1. DLL counts
$rfcAppDlls = (Get-ChildItem C:\RfcApp -Filter "*.dll" -ErrorAction SilentlyContinue).Count
$out += "C:\RfcApp DLLs: $rfcAppDlls"

$workSrc = "C:\actions-runner\_work\rfc-api\rfc-api\bin\Release"
$workDlls = (Get-ChildItem $workSrc -Filter "*.dll" -ErrorAction SilentlyContinue).Count
$out += "Build output DLLs: $workDlls"

# 2. Copy from work dir if C:\RfcApp empty
if ($rfcAppDlls -eq 0 -and $workDlls -gt 0) {
    if (-not (Test-Path C:\RfcApp)) { New-Item -ItemType Directory C:\RfcApp -Force | Out-Null }
    Copy-Item "$workSrc\*" C:\RfcApp -Recurse -Force
    $rfcAppDlls = (Get-ChildItem C:\RfcApp -Filter "*.dll" -ErrorAction SilentlyContinue).Count
    $out += "After work-dir copy: $rfcAppDlls DLLs"
}

# 3. Copy Web.config
$wc = "C:\actions-runner\_work\rfc-api\rfc-api\Web.config"
if (Test-Path $wc) { Copy-Item $wc C:\RfcApp\ -Force -ErrorAction SilentlyContinue }
$out += "Web.config: $(Test-Path C:\RfcApp\Web.config)"

# 4. Key DLLs
$vendor = (Get-ChildItem C:\RfcApp -Filter "Vendor*.dll" -ErrorAction SilentlyContinue | Select -First 2 -ExpandProperty Name) -join ", "
$sap = (Get-ChildItem C:\RfcApp -Filter "*sap*" -ErrorAction SilentlyContinue | Select -First 2 -ExpandProperty Name) -join ", "
$out += "Vendor DLLs: $vendor"
$out += "SAP DLLs: $sap"

# 5. IIS
Import-Module WebAdministration
Set-ItemProperty "IIS:\AppPools\RfcApiPool" -Name managedRuntimeVersion -Value "v4.0" -ErrorAction SilentlyContinue
Restart-WebAppPool "RfcApiPool" -ErrorAction SilentlyContinue
Start-Sleep 6
$siteState = (Get-Website "RfcApiApp" -ErrorAction SilentlyContinue).State
$out += "RfcApiApp state: $siteState"

# 6. appsettings
$cfg = "C:\RfcCollector\appsettings.json"
$json = Get-Content $cfg | ConvertFrom-Json
$json.IIS_HOST = "127.0.0.1"
$json.IIS_PORT = "8888"
$json | ConvertTo-Json -Depth 10 | Set-Content $cfg
$out += "Config: IIS_HOST=$($json.IIS_HOST) PORT=$($json.IIS_PORT)"

# 7. Restart service
Restart-Service RfcCollector -Force -ErrorAction SilentlyContinue
Start-Sleep 12
$svc = (Get-Service RfcCollector -ErrorAction SilentlyContinue).Status
$out += "RfcCollector: $svc"

# 8. IIS health
try {
    $h = Invoke-WebRequest "http://localhost:8888/api/ZAdvancePaymentRfc/Health" -UseBasicParsing -TimeoutSec 15
    $out += "IIS Health: HTTP $($h.StatusCode) $($h.Content)"
} catch {
    $out += "IIS Health ERROR: $($_.Exception.Message)"
}

# 9. E2E
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
