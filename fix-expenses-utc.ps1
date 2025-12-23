param([string]$Root="C:\sami")

$prog = Join-Path $Root "Program.cs"
if(!(Test-Path $prog)){ throw "Program.cs not found: $prog" }

# Backup
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
Copy-Item $prog ($prog + ".bak_expensesUtc_" + $ts) -Force

$p = Get-Content $prog -Raw -Encoding UTF8

# 1) FIX POST /api/expenses : ensure Date is UTC kind + set UpdatedAt on insert
$patPost = '(?s)CreatedAt\s*=\s*DateTime\.UtcNow\s*,\s*(?:UpdatedAt\s*=\s*DateTime\.UtcNow\s*,\s*)?Date\s*=\s*body\.Date\.Date\s*,'
$repPost = "CreatedAt = DateTime.UtcNow,`r`n    UpdatedAt = DateTime.UtcNow,`r`n    Date = DateTime.SpecifyKind(body.Date.Date, DateTimeKind.Utc),"
$p2 = [regex]::Replace($p, $patPost, $repPost, 1)

# 2) FIX PUT /api/expenses/{id:int} : ensure Date is UTC kind
$patPut = 'e\.Date\s*=\s*body\.Date\.Date\s*;'
$repPut = 'e.Date = DateTime.SpecifyKind(body.Date.Date, DateTimeKind.Utc);'
$p2 = [regex]::Replace($p2, $patPut, $repPut)

if($p2 -eq $p){
  Write-Host "No changes were needed (patterns not found or already fixed)."
}else{
  Set-Content -Path $prog -Encoding UTF8 -Value $p2
  Write-Host "✅ Expenses UTC fix applied to Program.cs"
}

