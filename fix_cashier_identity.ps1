param()

function ReadUtf8($p){ [IO.File]::ReadAllText($p, [Text.Encoding]::UTF8) }
function WriteUtf8($p,$t){ [IO.File]::WriteAllText($p, $t, [Text.Encoding]::UTF8) }

$prog = ".\Program.cs"
$cash = ".\wwwroot\cashier.html"

Copy-Item $prog "$prog.bak_cashierfix_$(Get-Date -Format yyyyMMdd_HHmmss)" -Force
Copy-Item $cash "$cash.bak_cashierfix_$(Get-Date -Format yyyyMMdd_HHmmss)" -Force

# --------------------------
# 1) Patch Program.cs (save invoice: set CashierId/CashierName from header x-cashier-id and Employees)
# --------------------------
$txt = ReadUtf8 $prog

# safety: only patch if not already present
if($txt -notmatch 'x-cashier-id')
{
  $inject = @"
    // Cashier identity (safe): numeric header only; resolve name from Employees
    var hCashierId = http.Headers[""x-cashier-id""].FirstOrDefault();
    if (int.TryParse(hCashierId, out var cid))
    {
        inv.CashierId = cid;
        try
        {
            var empName = await db.Employees.AsNoTracking()
                .Where(e => e.Id == cid)
                .Select(e => e.Name)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(empName))
                inv.CashierName = empName;
        }
        catch { }
    }

"@

  # insert right after "var inv = new CashierInvoice { ... };"
  $rx = [regex]::new('var\s+inv\s*=\s*new\s+CashierInvoice\s*\{.*?\};', [System.Text.RegularExpressions.RegexOptions]::Singleline)
  $m = $rx.Match($txt)
  if(-not $m.Success){ throw "Could not find 'var inv = new CashierInvoice { ... };' in Program.cs" }

  $replacement = $m.Value + "`r`n" + $inject
  $txt = $rx.Replace($txt, [System.Text.RegularExpressions.MatchEvaluator]{ param($mm) $replacement }, 1)
}

# 2) Patch Program.cs (report endpoint: include cashierId/cashierName in JSON)
# best-effort: add fields inside first Select(c => new { ... }) found in report endpoint section
if($txt -notmatch 'cashierName\s*=\s*c\.CashierName')
{
  $rxSel = [regex]::new('Select\s*\(\s*c\s*=>\s*new\s*\{\s*', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
  $txt = $rxSel.Replace($txt, 'Select(c => new { cashierId = c.CashierId, cashierName = c.CashierName, ', 1)
}

WriteUtf8 $prog $txt
Write-Host "OK: Program.cs patched (cashier saved + report includes cashier fields)." -ForegroundColor Green


# --------------------------
# 3) Patch cashier.html (send x-cashier-id header; NO Arabic in headers)
# --------------------------
$h = ReadUtf8 $cash

# add helper only if not present
if($h -notmatch 'function\s+__getCashierId')
{
  $helper = @"
<script>
  // Cashier Id helper (safe): numeric only (avoid Arabic in headers)
  async function __getCashierId(){
    // 1) try cached
    try{
      const cached = localStorage.getItem("cashier_id");
      if(cached && /^\d+$/.test(cached)) return cached;
    }catch{}

    // 2) try auth/me if token exists
    try{
      const token = localStorage.getItem("emp_token") || "";
      if(!token) return "";
      const r = await fetch("/api/auth/me", { headers: { "Authorization": "Bearer " + token } });
      if(!r.ok) return "";
      const me = await r.json();
      const id = (me && (me.id ?? me.employeeId ?? me.empId));
      if(id==null) return "";
      const sid = String(id);
      if(!/^\d+$/.test(sid)) return "";
      localStorage.setItem("cashier_id", sid);
      return sid;
    }catch{ return ""; }
  }
</script>

"@

  # insert helper before </head>
  if($h -match '</head>')
  {
    $h = $h -replace '</head>', ($helper + '</head>')
  }
}

# Now inject header in saveInvoice fetch
# Replace headers block { "Content-Type": "application/json" } with include x-cashier-id
$h = $h -replace 'headers:\s*\{\s*"Content-Type"\s*:\s*"application\/json"\s*\}',
'headers: Object.assign({ "Content-Type": "application/json" }, (await __getCashierId()) ? { "x-cashier-id": (await __getCashierId()) } : {})'

WriteUtf8 $cash $h
Write-Host "OK: cashier.html patched (sends x-cashier-id safely)." -ForegroundColor Green

Write-Host "`nDone. Now rebuild + run." -ForegroundColor Cyan
