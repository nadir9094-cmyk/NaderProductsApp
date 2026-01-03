$ErrorActionPreference="Stop"
$base="http://127.0.0.1:5050"

function Ok($m){Write-Host "✅ $m" -ForegroundColor Green}
function Warn($m){Write-Host "🟡 $m" -ForegroundColor Yellow}
function Bad($m){Write-Host "❌ $m" -ForegroundColor Red; throw $m}

# Ping
try{ (iwr "$base/" -UseBasicParsing -TimeoutSec 8) | Out-Null; Ok "Server reachable" } catch { Bad "Server not reachable (شغّل dotnet run)" }

# Login
$login = Invoke-RestMethod "$base/api/auth/login" -Method POST -ContentType "application/json" -Body (@{username="admin";password="admin123"}|ConvertTo-Json -Compress)
if(-not $login.token){ Bad "Login OK but token missing" }
$h=@{Authorization="Bearer "+$login.token}
Ok "Login OK"

# Auth me
try{ Invoke-RestMethod "$base/api/auth/me" -Headers $h -TimeoutSec 10 | Out-Null; Ok "Auth me OK" } catch { Bad "Auth me failed" }

# Employees
try{ Invoke-RestMethod "$base/api/employees" -Headers $h -TimeoutSec 10 | Out-Null; Ok "Employees OK" } catch { Bad "Employees failed (صلاحيات؟)" }

# Shifts: open (tolerate already open), current, close
try{
  $openOk=$false
  try{
    Invoke-RestMethod "$base/api/shifts/open" -Method POST -Headers $h -ContentType "application/json" -Body (@{openingCash=0}|ConvertTo-Json -Compress) -TimeoutSec 10 | Out-Null
    Ok "Shift opened"
    $openOk=$true
  } catch {
    # Read body text (SHIFT_ALREADY_OPEN)
    $txt=""
    try{
      $sr=New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())
      $txt=$sr.ReadToEnd()
    } catch {}
    if($txt -match "SHIFT_ALREADY_OPEN"){
      Warn "Shift already open (هذا طبيعي)"
    } else {
      throw
    }
  }

  $cur = Invoke-RestMethod "$base/api/shifts/current" -Headers $h -TimeoutSec 10
  if(-not $cur.hasShift){ Bad "No current shift returned" }
  Ok ("Shift current OK (#"+$cur.id+")")

  # close (always safe)
  Invoke-RestMethod "$base/api/shifts/close" -Method POST -Headers $h -ContentType "application/json" -Body (@{countedCash=0;adjustment=0;note="smoke test"}|ConvertTo-Json -Compress) -TimeoutSec 10 | Out-Null
  Ok "Shift closed OK"
} catch {
  Bad ("Shifts failed: " + $_.Exception.Message)
}

Ok "🎉 All smoke tests passed"
