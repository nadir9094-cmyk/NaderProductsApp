$ErrorActionPreference="Stop"

$progPath = "C:\sami\Program.cs"
if(-not (Test-Path $progPath)){ throw "Program.cs غير موجود" }

Copy-Item $progPath "$progPath.bak_before_types_split" -Force

$lines = Get-Content $progPath -Encoding UTF8

# خذ using من أعلى الملف (عشان الملف الجديد يترجم بدون صداع)
$usingLines = @()
foreach($ln in $lines){
  if($ln -match '^\s*(global\s+using|using)\s+' -or $ln.Trim() -eq ''){
    $usingLines += $ln
    continue
  }
  break
}
$usingLines = $usingLines | Select-Object -Unique

# Regex لبداية Type أو namespace على مستوى السطر
$rxTypeStart = '^\s*(public|internal|private|protected)?\s*(sealed\s+|static\s+)?(partial\s+)?(class|record|struct|interface|enum)\b'
$rxNsStart   = '^\s*namespace\s+\S+'

$main = New-Object System.Collections.Generic.List[string]
$types = New-Object System.Collections.Generic.List[string]

$inBlock = $false
$brace = 0
$seenBrace = $false

function Count-Char([string]$s, [char]$c){
  ($s.ToCharArray() | Where-Object { $_ -eq $c }).Count
}

for($i=0; $i -lt $lines.Count; $i++){
  $ln = $lines[$i]

  if(-not $inBlock){
    if($ln -match $rxTypeStart -or $ln -match $rxNsStart){
      $inBlock = $true
      $brace = 0
      $seenBrace = $false

      $types.Add($ln)

      $opens = Count-Char $ln '{'
      $closes = Count-Char $ln '}'
      if($opens -gt 0){ $seenBrace = $true }
      $brace += ($opens - $closes)

      # نوع سطر واحد مثل: public record X(...);
      if(-not $seenBrace -and $ln.TrimEnd().EndsWith(';')){
        $inBlock = $false
      }

      continue
    }
    $main.Add($ln)
    continue
  }

  # داخل بلوك Type/namespace
  $types.Add($ln)

  $opens = Count-Char $ln '{'
  $closes = Count-Char $ln '}'
  if($opens -gt 0){ $seenBrace = $true }
  $brace += ($opens - $closes)

  if($seenBrace -and $brace -le 0){
    $inBlock = $false
  }
}

# نظّف Program.cs: احذف الفراغات الزائدة آخر الملف + تأكد فيه تشغيل
$mainText = ($main -join "`r`n").TrimEnd() + "`r`n"
if($mainText -notmatch 'app\.Run(Async)?\s*\('){
  $mainText += "`r`napp.Run();`r`n"
}

Set-Content -Encoding UTF8 -Path $progPath -Value $mainText

# اكتب الأنواع في ملف منفصل
$typesPath = "C:\sami\Program.Types.cs"
$typesText = ($usingLines -join "`r`n") + "`r`n`r`n" + ($types -join "`r`n") + "`r`n"
Set-Content -Encoding UTF8 -Path $typesPath -Value $typesText

Write-Host "✅ OK: Split types out of Program.cs"
Write-Host "Backup: $progPath.bak_before_types_split"
Write-Host "New file: $typesPath"
