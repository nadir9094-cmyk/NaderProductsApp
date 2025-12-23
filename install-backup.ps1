$ErrorActionPreference="Stop"
$root="C:\sami"
$www=Join-Path $root "wwwroot"
New-Item -ItemType Directory -Force -Path $www | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $www "backups") | Out-Null

$backupHtml=@"
<!doctype html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>النسخ الاحتياطي</title>
</head>
<body style="font-family:Tahoma; padding:20px">
  <h2>💾 النسخ الاحتياطي</h2>
  <p>تم إنشاء الصفحة بنجاح.</p>
</body>
</html>
"@

Set-Content -Encoding UTF8 -Path (Join-Path $www "backup.html") -Value $backupHtml
Write-Host "✅ OK: $www\backup.html"
