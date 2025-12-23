param([string]$Root="C:\sami")

Write-Host "== NADER RECOVER ==" -ForegroundColor Cyan

# 1) Ensure Root exists
if(!(Test-Path $Root)){ throw "Root not found: $Root" }

# 2) Find latest Program.cs backup
$progBak = Get-ChildItem $Root -File -Filter "Program.cs.bak_*" -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1

if(!$progBak){
  # fallback: any Program.cs.*bak*
  $progBak = Get-ChildItem $Root -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^Program\.cs\..*bak' -or $_.Name -match '^Program\.cs\.bak' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

if(!$progBak){
  throw "ما لقيت أي نسخة احتياطية لـ Program.cs داخل $Root (ملفات Program.cs.bak_*)"
}

# 3) Restore Program.cs
Copy-Item $progBak.FullName (Join-Path $Root "Program.cs") -Force
Write-Host ("Restored Program.cs from: " + $progBak.Name) -ForegroundColor Green

# 4) Remove duplicate SettingsDto.cs if exists (سبب CS0101 غالباً)
$settingsDto = Join-Path $Root "SettingsDto.cs"
if(Test-Path $settingsDto){
  $ts = Get-Date -Format "yyyyMMdd_HHmmss"
  $archiveDir = Join-Path $Root "RECOVER_ARCHIVE"
  New-Item -ItemType Directory -Force -Path $archiveDir | Out-Null
  Move-Item $settingsDto (Join-Path $archiveDir ("SettingsDto.cs.DUPLICATE_" + $ts)) -Force
  Write-Host "Moved duplicate SettingsDto.cs to RECOVER_ARCHIVE" -ForegroundColor Yellow
}

# 5) Restore backup.html if there's a backup (optional)
$www = Join-Path $Root "wwwroot"
if(Test-Path $www){
  $bhBak = Get-ChildItem $www -File -Filter "backup.html.bak_*" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if($bhBak){
    Copy-Item $bhBak.FullName (Join-Path $www "backup.html") -Force
    Write-Host ("Restored backup.html from: " + $bhBak.Name) -ForegroundColor Green
  }
}

Write-Host "`nDONE. Now rebuilding..." -ForegroundColor Cyan
