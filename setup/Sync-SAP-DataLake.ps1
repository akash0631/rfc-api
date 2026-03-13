Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force

param(
    [string]$CompanyCode  = "1000",
    [string]$DateFrom     = "20250101",
    [string]$DateTo       = "20260313"
)

$SQL_SERVER  = "192.168.151.28,1433"
$SQL_DB      = "DataV2"
$SQL_USER    = "sa"
$SQL_PASS    = "vrl@55555"
$IIS_BASE    = "http://localhost"
$DAB_BASE    = "https://my-dab-app.azurewebsites.net"
$RFC_NAME    = "ZADVANCE_PAYMENT_RFC"
$TABLE_NAME  = "ET_ZADVANCE_PAYMENT"

Write-Host "═══════════════════════════════════════════"
Write-Host "  V2 Data Lake Sync — $RFC_NAME"
Write-Host "═══════════════════════════════════════════"

# ── STEP 1: Create SQL table if not exists ─────────────────────────────────
Write-Host "`n[1/4] Creating table $TABLE_NAME if not exists..."

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=$SQL_SERVER;Database=$SQL_DB;User Id=$SQL_USER;Password=$SQL_PASS;TrustServerCertificate=True;"
$conn.Open()

$createSQL = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '$TABLE_NAME')
BEGIN
    CREATE TABLE [dbo].[$TABLE_NAME] (
        [ID]              INT IDENTITY(1,1) PRIMARY KEY,
        [DOCUMENT_TYPE]   NVARCHAR(10)  NULL,
        [COMPANY_CODE]    NVARCHAR(10)  NULL,
        [DOCUMENT_NUMBER] NVARCHAR(20)  NULL,
        [FISCAL_YEAR]     NVARCHAR(10)  NULL,
        [LINE_ITEM]       NVARCHAR(10)  NULL,
        [POSTING_KEY]     NVARCHAR(10)  NULL,
        [ACCOUNT_TYPE]    NVARCHAR(10)  NULL,
        [SPECIAL_G_L_IND] NVARCHAR(10)  NULL,
        [TRANSACT_TYPE]   NVARCHAR(20)  NULL,
        [DEBIT_CREDIT]    NVARCHAR(5)   NULL,
        [AMOUNT_IN_LC]    DECIMAL(18,2) NULL,
        [AMOUNT]          DECIMAL(18,2) NULL,
        [TEXT]            NVARCHAR(200) NULL,
        [VENDOR]          NVARCHAR(20)  NULL,
        [PAYMENT_AMT]     DECIMAL(18,2) NULL,
        [POSTING_DATE]    DATE          NULL,
        [_RFC]            NVARCHAR(50)  NULL DEFAULT '$RFC_NAME',
        [_SYNC_AT]        DATETIME      NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_${TABLE_NAME}_CO   ON [dbo].[$TABLE_NAME] ([COMPANY_CODE]);
    CREATE INDEX IX_${TABLE_NAME}_DATE ON [dbo].[$TABLE_NAME] ([POSTING_DATE]);
    CREATE INDEX IX_${TABLE_NAME}_VND  ON [dbo].[$TABLE_NAME] ([VENDOR]);
    PRINT 'Table created.'
END
ELSE
    PRINT 'Table already exists.'
"@

$cmd = $conn.CreateCommand()
$cmd.CommandText = $createSQL
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "  Table OK" -ForegroundColor Green

# ── STEP 2: Call SAP RFC via local IIS endpoint ────────────────────────────
Write-Host "`n[2/4] Calling SAP RFC via IIS ($IIS_BASE/api/$RFC_NAME)..."

$body = @{
    I_COMPANY_CODE      = $CompanyCode
    I_POSTING_DATE_LOW  = $DateFrom
    I_POSTING_DATE_HIGH = $DateTo
} | ConvertTo-Json

try {
    $resp = Invoke-RestMethod -Uri "$IIS_BASE/api/$RFC_NAME" `
        -Method POST -Body $body -ContentType "application/json" -TimeoutSec 120
    $rows = $resp.IT_FINAL
    if (-not $rows) { $rows = $resp }
    Write-Host "  SAP returned $($rows.Count) rows" -ForegroundColor Green
} catch {
    Write-Host "  IIS call failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

if ($rows.Count -eq 0) {
    Write-Host "  No data returned from SAP — check date range or company code" -ForegroundColor Yellow
    exit 0
}

# ── STEP 3: Upsert rows into SQL ───────────────────────────────────────────
Write-Host "`n[3/4] Upserting $($rows.Count) rows into DataV2.dbo.$TABLE_NAME..."

# Truncate first for a clean sync (or use MERGE for incremental)
$cmd.CommandText = "TRUNCATE TABLE [dbo].[$TABLE_NAME]"
$cmd.ExecuteNonQuery() | Out-Null

$inserted = 0
foreach ($row in $rows) {
    $sql = @"
INSERT INTO [dbo].[$TABLE_NAME]
  (DOCUMENT_TYPE,COMPANY_CODE,DOCUMENT_NUMBER,FISCAL_YEAR,LINE_ITEM,
   POSTING_KEY,ACCOUNT_TYPE,SPECIAL_G_L_IND,TRANSACT_TYPE,DEBIT_CREDIT,
   AMOUNT_IN_LC,AMOUNT,[TEXT],VENDOR,PAYMENT_AMT,POSTING_DATE,_RFC,_SYNC_AT)
VALUES
  (@dt,@co,@dn,@fy,@li,@pk,@at,@sg,@tt,@dc,@al,@am,@tx,@vn,@pa,@pd,'$RFC_NAME',GETDATE())
"@
    $c2 = $conn.CreateCommand()
    $c2.CommandText = $sql
    $c2.Parameters.AddWithValue("@dt", [string]$row.DOCUMENT_TYPE)  | Out-Null
    $c2.Parameters.AddWithValue("@co", [string]$row.COMPANY_CODE)   | Out-Null
    $c2.Parameters.AddWithValue("@dn", [string]$row.DOCUMENT_NUMBER)| Out-Null
    $c2.Parameters.AddWithValue("@fy", [string]$row.FISCAL_YEAR)    | Out-Null
    $c2.Parameters.AddWithValue("@li", [string]$row.LINE_ITEM)      | Out-Null
    $c2.Parameters.AddWithValue("@pk", [string]$row.POSTING_KEY)    | Out-Null
    $c2.Parameters.AddWithValue("@at", [string]$row.ACCOUNT_TYPE)   | Out-Null
    $c2.Parameters.AddWithValue("@sg", [string]$row.SPECIAL_G_L_IND)| Out-Null
    $c2.Parameters.AddWithValue("@tt", [string]$row.TRANSACT_TYPE)  | Out-Null
    $c2.Parameters.AddWithValue("@dc", [string]$row.DEBIT_CREDIT)   | Out-Null
    $amtLC = try { [decimal]$row.AMOUNT_IN_LC } catch { 0 }
    $amt   = try { [decimal]$row.AMOUNT }       catch { 0 }
    $pmtA  = try { [decimal]$row.PAYMENT_AMT }  catch { 0 }
    $c2.Parameters.AddWithValue("@al", $amtLC)  | Out-Null
    $c2.Parameters.AddWithValue("@am", $amt)    | Out-Null
    $c2.Parameters.AddWithValue("@tx", [string]$row.TEXT)           | Out-Null
    $c2.Parameters.AddWithValue("@vn", [string]$row.VENDOR)         | Out-Null
    $c2.Parameters.AddWithValue("@pa", $pmtA)   | Out-Null
    $pd = try { [datetime]::ParseExact([string]$row.POSTING_DATE,"yyyyMMdd",$null) } catch { [DBNull]::Value }
    $c2.Parameters.AddWithValue("@pd", $pd)     | Out-Null
    $c2.ExecuteNonQuery() | Out-Null
    $inserted++
}

$conn.Close()
Write-Host "  Inserted $inserted rows" -ForegroundColor Green

# ── STEP 4: Verify via DAB REST API ───────────────────────────────────────
Write-Host "`n[4/4] Verifying API endpoint..."
Start-Sleep -Seconds 3

try {
    $check = Invoke-RestMethod -Uri "$DAB_BASE/api/$TABLE_NAME" -Method GET -TimeoutSec 15
    $count = if ($check.value) { $check.value.Count } else { "N/A" }
    Write-Host "  API live! Rows visible: $count" -ForegroundColor Green
    Write-Host "  URL: $DAB_BASE/api/$TABLE_NAME" -ForegroundColor Cyan
} catch {
    Write-Host "  API check: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n✅ SYNC COMPLETE — $inserted rows from SAP now in DataV2 + live on DAB API" -ForegroundColor Green
