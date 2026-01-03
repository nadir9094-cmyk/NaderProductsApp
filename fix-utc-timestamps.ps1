$ErrorActionPreference="Stop"

# عدّل كل ملفات .cs داخل المشروع (بدون مجلدات bin/obj)
$files = Get-ChildItem -Path . -Recurse -File -Filter "*.cs" |
  Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" }

$changed = 0
foreach($f in $files){
  $txt = Get-Content $f.FullName -Raw -Encoding UTF8

  $new = $txt

  # 1) أهم شي: DateTime.Now -> DateTime.UtcNow
  $new = [regex]::Replace($new, '\bDateTime\.Now\b', 'DateTime.UtcNow')

  # 2) DateTime.Today -> DateTime.UtcNow.Date
  $new = [regex]::Replace($new, '\bDateTime\.Today\b', 'DateTime.UtcNow.Date')

  # 3) (اختياري) DateTimeOffset.Now -> DateTimeOffset.UtcNow
  $new = [regex]::Replace($new, '\bDateTimeOffset\.Now\b', 'DateTimeOffset.UtcNow')

  if($new -ne $txt){
    Set-Content -Path $f.FullName -Encoding UTF8 -Value $new
    $changed++
  }
}

"CHANGED_FILES=$changed"

dotnet build
