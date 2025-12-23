param([string]$Root="C:\sami")

$prog = Join-Path $Root "Program.cs"
if(!(Test-Path $prog)){ throw "Program.cs غير موجود في: $prog" }

Write-Host "1) البحث عن آخر نسخة Program.cs احتياطية..." -ForegroundColor Cyan

# ابحث عن آخر نسخة احتياطية لبرنامج.cs (أنت عندك كثير .bak_*)
$bak = Get-ChildItem $Root -File -Filter "Program.cs.bak_*" -ErrorAction SilentlyContinue |
       Sort-Object LastWriteTime -Descending |
       Select-Object -First 1

if(!$bak){
  throw "ما لقيت أي Program.cs.bak_* — لازم ترجع ملف Program.cs الصحيح من نسخة كانت شغالة."
}

# خذ لقطة إضافية من الوضع الحالي قبل الاسترجاع
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
Copy-Item $prog ($prog + ".BROKEN_" + $ts) -Force

Write-Host ">> Restore Program.cs from: $($bak.Name)" -ForegroundColor Yellow
Copy-Item $bak.FullName $prog -Force

# 2) حل مشكلة SettingsDto المكرر
$settingsFile = Join-Path $Root "SettingsDto.cs"
if(Test-Path $settingsFile){
  $p = Get-Content $prog -Raw -Encoding UTF8
  $hasInProgram = ($p -match '\b(class|record)\s+SettingsDto\b')
  if($hasInProgram){
    $dest = Join-Path $Root ("SettingsDto.DISABLED_" + $ts + ".cs")
    Move-Item $settingsFile $dest -Force
    Write-Host ">> عطّلت SettingsDto.cs لأنه مكرر داخل Program.cs => $dest" -ForegroundColor Yellow
  } else {
    Write-Host ">> SettingsDto.cs موجود لكن Program.cs لا يحتوي SettingsDto — تركته كما هو." -ForegroundColor Green
  }
}

Write-Host "3) تنظيف وبناء..." -ForegroundColor Cyan
Remove-Item -Recurse -Force (Join-Path $Root "bin"),(Join-Path $Root "obj") -ErrorAction SilentlyContinue | Out-Null

Write-Host "`n✅ تم. الآن شغّل:" -ForegroundColor Green
Write-Host "dotnet build" -ForegroundColor Green
Write-Host "dotnet run" -ForegroundColor Green
