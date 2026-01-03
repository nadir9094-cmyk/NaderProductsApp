$ErrorActionPreference="Stop"

$svc="postgresql-x64-16"
$datadir="C:\Program Files\PostgreSQL\16\data"
$hba=Join-Path $datadir "pg_hba.conf"
$psql="C:\Program Files\PostgreSQL\16\bin\psql.exe"

if(!(Test-Path $hba)){ throw "pg_hba.conf not found: $hba" }
if(!(Test-Path $psql)){ throw "psql.exe not found: $psql" }

# stop service
Stop-Service $svc -Force

# backup
$bak="$hba.bak_{0}" -f (Get-Date -Format "yyyyMMdd_HHmmss")
Copy-Item $hba $bak -Force

# prepend TRUST (local only) so we can login without passwords
$prepend=@(
"local   all             all                                     trust",
"host    all             all            127.0.0.1/32             trust",
"host    all             all            ::1/128                  trust",
""
)
($prepend + (Get-Content $hba)) | Set-Content -Encoding ASCII $hba

# start service
Start-Service $svc
Start-Sleep -Seconds 2

# set password for nader on port 5433
& $psql -U postgres -h 127.0.0.1 -p 5433 -d postgres -c "ALTER ROLE nader WITH PASSWORD '123456';" | Out-Host

# restore original pg_hba
Copy-Item $bak $hba -Force
Restart-Service $svc -Force
Start-Sleep -Seconds 1

Write-Host "DONE ✅ nader password is now 123456 (port 5433)" -ForegroundColor Green
Write-Host "Backup: $bak" -ForegroundColor DarkGray
