$ErrorActionPreference="Stop"
Set-Location "C:\sami"

# files
$program = ".\Program.cs"
$snippetPath = ".\_shift_api_snippet.cs.txt"
$shiftsHtmlSrc = ".\_shifts.html"
$wwwroot = ".\wwwroot"
$shiftsHtmlDest = Join-Path $wwwroot "shifts.html"

# 0) backup
Copy-Item $program "$program.bak_$(Get-Date -Format yyyyMMdd_HHmmss)" -Force

# 1) drop helper files (snippet + html) into C:\sami from the same folder you run this script
if(!(Test-Path $snippetPath)){
  Write-Host "Missing $snippetPath . Put _shift_api_snippet.cs.txt beside this script." -ForegroundColor Yellow
  exit 1
}
if(!(Test-Path $shiftsHtmlSrc)){
  Write-Host "Missing $shiftsHtmlSrc . Put _shifts.html beside this script." -ForegroundColor Yellow
  exit 1
}
if(!(Test-Path $wwwroot)){ New-Item -ItemType Directory -Path $wwwroot | Out-Null }
Copy-Item $shiftsHtmlSrc $shiftsHtmlDest -Force
Write-Host "OK: wwwroot\shifts.html updated."

# 2) patch Program.cs
$txt = Get-Content $program -Raw -Encoding UTF8
if($txt -match "SHIFTS \(Daily closing\) API"){
  Write-Host "Program.cs already contains shift API. Skipping insert." -ForegroundColor Cyan
} else {
  $snippet = Get-Content $snippetPath -Raw -Encoding UTF8

  # ensure Npgsql using (only if your Program.cs has top-level statements with usings)
  if($txt -match "^\s*using\s+Npgsql\s*;" -notmatch $true){
    if($txt -match "^\s*using\s+"){
      $txt = $txt -replace "(\A(?:\s*using[^\r\n]*\r?\n)+)", ('$1' + "using Npgsql;`r`n")
    }
  }

  # insert snippet before app.Run();
  if($txt -notmatch "(?m)^\s*app\.Run\(\);\s*$"){
    Write-Host "Could not find app.Run(); in Program.cs" -ForegroundColor Red
    exit 1
  }
  $txt = [regex]::Replace($txt, "(?m)^\s*app\.Run\(\);\s*$", ($snippet + "`r`n`r`napp.Run();"), 1)
  Set-Content $program -Value $txt -Encoding UTF8
  Write-Host "OK: Shift API inserted into Program.cs"
}

Write-Host "`nNext: dotnet build && dotnet run" -ForegroundColor Green
