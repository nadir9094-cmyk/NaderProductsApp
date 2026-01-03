# Fix PG auth + set passwords (PostgreSQL 16, data dir default)
$ErrorActionPreference = "Stop"

$svc = "postgresql-x64-16"
$datadir = "C:\Program Files\PostgreSQL\16\data"
$hba = Join-Path $datadir "pg_hba.conf"
$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"

if(!(Test-Path $hba)){ throw "pg_hba.conf not found: $hba" }
if(!(Test-Path $psql)){ throw "psql.exe not found: $psql" }

Write-Host "Stopping service..." -ForegroundColor Yellow
Stop-Service $svc -Force

# Backup hba
$bak = "$hba.bak_{0}" -f (Get-Date -Format "yyyyMMdd_HHmmss")
Copy-Item $hba $bak -Force

# Read and set TRUST for local/loopback
$lines = Get-Content $hba
$lines = $lines | ForEach-Object {
  if($_ -match '^\s*host\s+all\s+all\s+127\.0\.0\.1/32\s+') { 'host all all 127.0.0.1/32 trust' }
  elseif($_ -match '^\s*host\s+all\s+all\s+::1/128\s+') { 'host all all ::1/128 trust' }
  elseif($_ -match '^\s*local\s+all\s+all\s+') { 'local all all trust' }
  else { $_ }
}
$lines | Set-Content -Encoding ASCII $hba

Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service $svc
Start-Sleep -Seconds 2

Write-Host "Setting passwords on port 5433..." -ForegroundColor Yellow
& $psql -U postgres -h 127.0.0.1 -p 5433 -d postgres -c "ALTER ROLE postgres WITH PASSWORD '123456';" | Out-Host
& $psql -U postgres -h 127.0.0.1 -p 5433 -d postgres -c "ALTER ROLE nader WITH PASSWORD 'Nader@12345';" | Out-Host

# Restore secure auth to scram-sha-256
$lines2 = Get-Content $hba
$lines2 = $lines2 | ForEach-Object {
  if($_ -match '^\s*host\s+all\s+all\s+127\.0\.0\.1/32\s+trust\s*$') { 'host all all 127.0.0.1/32 scram-sha-256' }
  elseif($_ -match '^\s*host\s+all\s+all\s+::1/128\s+trust\s*$') { 'host all all ::1/128 scram-sha-256' }
  elseif($_ -match '^\s*local\s+all\s+all\s+trust\s*$') { 'local all all scram-sha-256' }
  else { $_ }
}
$lines2 | Set-Content -Encoding ASCII $hba

Write-Host "Restarting service..." -ForegroundColor Yellow
Restart-Service $svc -Force
Start-Sleep -Seconds 1

Write-Host "DONE ✅ postgres=123456 | nader=Nader@12345 | port=5433" -ForegroundColor Green
Write-Host "Backup saved: $bak" -ForegroundColor DarkGray
