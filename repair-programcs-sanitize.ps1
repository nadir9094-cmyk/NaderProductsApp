param()

$prog = "C:\sami\Program.cs"
if(!(Test-Path $prog)){ throw "Program.cs not found: $prog" }

$stamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
Copy-Item $prog "$prog.sanitize_$stamp.bak" -Force

# اقرأ الملف كسطور
$lines = Get-Content $prog -Encoding UTF8

function Remove-SectionByMarkers([string[]]$arr, [string]$startContains, [string]$endContains){
  $start = -1
  for($i=0; $i -lt $arr.Count; $i++){
    if($arr[$i] -like "*$startContains*"){ $start = $i; break }
  }
  if($start -lt 0){ return $arr } # ما لقينا البداية

  $end = -1
  for($j=$start; $j -lt $arr.Count; $j++){
    if($arr[$j] -like "*$endContains*"){ $end = $j; break }
  }

  if($end -lt 0){
    # لو ما لقينا النهاية: نحذف من البداية إلى قبل app.Run إن وجد، وإلا للنهاية
    $run = -1
    for($k=$start; $k -lt $arr.Count; $k++){
      if($arr[$k] -match '^\s*app\.Run\s*\('){ $run = $k; break }
    }
    if($run -gt 0){ $end = $run - 1 } else { $end = $arr.Count - 1 }
  }

  $out = New-Object System.Collections.Generic.List[string]
  for($i=0; $i -lt $arr.Count; $i++){
    if($i -ge $start -and $i -le $end){ continue }
    $out.Add($arr[$i])
  }
  return $out.ToArray()
}

# 1) احذف بلوكات SQLite المكسورة بالكامل (المشكلة عندك هنا)
$lines = Remove-SectionByMarkers $lines "SUPPLIERS STORE (PERSISTED)" "END SUPPLIERS STORE"
$lines = Remove-SectionByMarkers $lines "SETTINGS (PERSISTED)" "END SETTINGS"

# 2) تعقيم إضافي: احذف أي سطر يتيم واضح الخراب (زي اللي عندك بالسطر 518)
#    سطر عبارة عن ' فقط، أو " فقط، أو يبدأ بـ WHERE NOT EXISTS خارج سياق
$clean = New-Object System.Collections.Generic.List[string]
foreach($ln in $lines){
  $t = $ln.Trim()
  if($t -eq "'" -or $t -eq '"' -or $t -match '^\s*WHERE\s+NOT\s+EXISTS\s*\('){
    continue
  }
  $clean.Add($ln)
}
$lines = $clean.ToArray()

# 3) رتّب using: اجمع كل using من أي مكان وحطها فوق (حل CS1529)
$allUsings = New-Object System.Collections.Generic.List[string]
$body = New-Object System.Collections.Generic.List[string]
foreach($ln in $lines){
  if($ln -match '^\s*using\s+[^;]+;\s*$'){
    $u = ($ln.TrimEnd())
    if(-not $allUsings.Contains($u)){ $allUsings.Add($u) }
  } else {
    $body.Add($ln)
  }
}

# خلي usings في الأعلى بنفس ترتيبها (بدون ترتيب أبجدي حتى ما نخرب global usings)
$out = New-Object System.Collections.Generic.List[string]
foreach($u in $allUsings){ $out.Add($u) }
$out.Add("") # سطر فاصل
foreach($ln in $body){ $out.Add($ln) }

# 4) اكتب الملف
Set-Content -Path $prog -Encoding UTF8 -Value ($out -join "`r`n")

Write-Host "OK ✅ Program.cs sanitized. Backup: $prog.sanitize_$stamp.bak" -ForegroundColor Green
