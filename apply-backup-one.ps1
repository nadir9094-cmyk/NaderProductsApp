param([string]$Root="C:\sami")

# -----------------------------
# 0) Disable patch/fix folders that contain .cs (SDK project includes **\*.cs recursively)
# -----------------------------
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$archive = Join-Path $Root ("_ARCHIVE_PATCHES_" + $ts)
New-Item -ItemType Directory -Force -Path $archive | Out-Null

Get-ChildItem -Path $Root -Directory -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match '^(?:_PATCH|_fix|_FIX|Nader_Backup|NaderFixPack|Nader_).*' -or $_.Name -match 'PATCH|fix' } |
  ForEach-Object {
    try {
      $hasCs = @(Get-ChildItem -Path $_.FullName -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue).Count -gt 0
      if($hasCs){
        Move-Item $_.FullName $archive -Force
        Write-Host ("[OK] Moved folder out of project: " + $_.Name)
      }
    } catch {}
  }

# -----------------------------
# 1) Paths + backups
# -----------------------------
$prog = Join-Path $Root "Program.cs"
$www  = Join-Path $Root "wwwroot"
$bh   = Join-Path $www  "backup.html"

if(!(Test-Path $prog)){ throw "Program.cs not found: $prog" }
if(!(Test-Path $www)){ throw "wwwroot not found: $www" }

Copy-Item $prog ($prog + ".bak_backup_api_" + $ts) -Force
if(Test-Path $bh){ Copy-Item $bh ($bh + ".bak_backup_ui_" + $ts) -Force }

# -----------------------------
# 2) Write clean backup.html (no template-literal traps / no syntax errors)
# -----------------------------
@"
<!doctype html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>النسخ الاحتياطي</title>
  <style>
    body{font-family:system-ui,Segoe UI,Tahoma,Arial; margin:20px; background:#f7f7f9}
    .card{background:#fff; border:1px solid #e6e6ef; border-radius:12px; padding:16px; box-shadow:0 6px 18px rgba(0,0,0,.05)}
    .row{display:flex; gap:10px; flex-wrap:wrap; align-items:center; justify-content:space-between}
    button{cursor:pointer; border:0; border-radius:10px; padding:10px 14px; font-weight:700}
    .btn{background:#1f7aec; color:#fff}
    .btn2{background:#111827; color:#fff}
    .muted{color:#6b7280}
    table{width:100%; border-collapse:collapse; margin-top:12px}
    th,td{padding:10px; border-bottom:1px solid #eee; text-align:right}
    a{color:#1f7aec; text-decoration:none; font-weight:700}
    code{background:#f3f4f6; padding:2px 6px; border-radius:6px}
  </style>
</head>
<body>
  <div class="card">
    <div class="row">
      <div>
        <h2 style="margin:0 0 6px 0">النسخ الاحتياطي</h2>
        <div class="muted">API: <code>/api/backup/list</code> - <code>/api/backup/run</code> - <code>/api/backup/download/{name}</code></div>
      </div>
      <div class="row">
        <button class="btn2" id="btnRefresh">تحديث</button>
        <button class="btn" id="btnRun">إنشاء نسخة الآن</button>
      </div>
    </div>

    <p id="status" class="muted" style="margin:10px 0 0 0"></p>

    <table>
      <thead>
        <tr>
          <th>الملف</th>
          <th>الحجم</th>
          <th>آخر تعديل</th>
          <th>تحميل</th>
        </tr>
      </thead>
      <tbody id="rows"></tbody>
    </table>
  </div>

<script>
(function(){
  async function api(path, options){
    const res = await fetch(path, Object.assign({
      headers: { "Content-Type":"application/json" }
    }, options || {}));
    if(!res.ok){
      const t = await res.text().catch(function(){ return ""; });
      throw new Error("HTTP " + res.status + " " + (t || res.statusText));
    }
    return res;
  }

  function fmtBytes(n){
    n = Number(n||0);
    if(n < 1024) return n + " B";
    const u = ["KB","MB","GB","TB"];
    let i=-1, v=n;
    while(v >= 1024 && i < u.length-1){ v/=1024; i++; }
    return v.toFixed(i<=0?1:2) + " " + u[i];
  }

  async function refresh(){
    const st = document.getElementById("status");
    st.textContent = "⏳ جاري جلب النسخ...";
    try{
      const j = await (await api("/api/backup/list")).json();
      const tbody = document.getElementById("rows");
      tbody.innerHTML = "";
      (j.files || []).forEach(function(f){
        const tr = document.createElement("tr");
        const name = f.name || "";
        const size = fmtBytes(f.size || 0);
        const mod  = f.modifiedLocal || "";
        const a = '<a href="/api/backup/download/' + encodeURIComponent(name) + '" target="_blank">تحميل</a>';
        tr.innerHTML = "<td>"+name+"</td><td>"+size+"</td><td>"+mod+"</td><td>"+a+"</td>";
        tbody.appendChild(tr);
      });
      st.textContent = "✅ تم. عدد النسخ: " + ((j.files||[]).length);
    }catch(e){
      st.textContent = "❌ " + (e && e.message ? e.message : e);
    }
  }

  async function runNow(){
    const st = document.getElementById("status");
    st.textContent = "⏳ جاري إنشاء النسخة...";
    try{
      const j = await (await api("/api/backup/run", { method:"POST" })).json();
      st.textContent = "✅ تم إنشاء: " + (j.file || "(unknown)") + (j.note ? " - " + j.note : "");
      await refresh();
    }catch(e){
      st.textContent = "❌ " + (e && e.message ? e.message : e);
    }
  }

  document.getElementById("btnRefresh").addEventListener("click", refresh);
  document.getElementById("btnRun").addEventListener("click", runNow);

  refresh();
})();
</script>
</body>
</html>
"@ | Set-Content -Encoding UTF8 -Path $bh

# -----------------------------
# 3) Inject Backup API endpoints into Program.cs (only if missing)
#    (No new classes/types => no duplicates)
# -----------------------------
$p = Get-Content $prog -Raw -Encoding UTF8
if([string]::IsNullOrWhiteSpace($p)){ throw "Program.cs read as empty." }

if($p -match 'MapGet\(\s*"/api/backup/list"'){
  Write-Host "[OK] Backup API already exists in Program.cs"
}
else {
  $snippet = @"

//
// BACKUP API (auto-injected)
//
app.MapGet("/api/backup/list", (IWebHostEnvironment env) =>
{
    var dir = System.IO.Path.Combine(env.ContentRootPath, "backups");
    System.IO.Directory.CreateDirectory(dir);

    var files = System.IO.Directory.GetFiles(dir, "*.sql")
        .Select(f => new System.IO.FileInfo(f))
        .OrderByDescending(fi => fi.LastWriteTimeUtc)
        .Select(fi => new {
            name = fi.Name,
            size = fi.Length,
            modifiedUtc = fi.LastWriteTimeUtc,
            modifiedLocal = fi.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        })
        .ToList();

    return Results.Ok(new { files });
});

app.MapGet("/api/backup/download/{name}", (string name, IWebHostEnvironment env) =>
{
    var dir = System.IO.Path.Combine(env.ContentRootPath, "backups");
    var safe = System.IO.Path.GetFileName(name);
    var file = System.IO.Path.Combine(dir, safe);
    if(!System.IO.File.Exists(file)) return Results.NotFound(new { error = "File not found" });
    return Results.File(file, "application/sql", safe);
});

app.MapPost("/api/backup/run", async (IWebHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
{
    var dir = System.IO.Path.Combine(env.ContentRootPath, "backups");
    System.IO.Directory.CreateDirectory(dir);

    var fileName = "backup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".sql";
    var outPath  = System.IO.Path.Combine(dir, fileName);

    // Try pg_dump if available; otherwise create placeholder so UI still works.
    string? pgDump = null;
    try {
        pgDump = Environment.GetEnvironmentVariable("PG_DUMP");
        if(string.IsNullOrWhiteSpace(pgDump)){
            pgDump = "pg_dump"; // rely on PATH
        }
    } catch {}

    // Try to pick a connection string
    var cs =
        cfg.GetConnectionString("Default") ??
        cfg.GetConnectionString("DefaultConnection") ??
        cfg["ConnectionStrings:Default"] ??
        cfg["ConnectionStrings:DefaultConnection"] ??
        cfg["DATABASE_URL"];

    if(string.IsNullOrWhiteSpace(cs)){
        await System.IO.File.WriteAllTextAsync(outPath, "-- No connection string found. Placeholder backup file.\n", ct);
        return Results.Ok(new { file = fileName, note = "No connection string found (placeholder created)." });
    }

    // If no pg_dump, placeholder
    if(string.IsNullOrWhiteSpace(pgDump)){
        await System.IO.File.WriteAllTextAsync(outPath, "-- pg_dump not configured. Placeholder backup file.\n", ct);
        return Results.Ok(new { file = fileName, note = "pg_dump not configured (placeholder created)." });
    }

    // Build process args (best-effort). If fails, fallback to placeholder.
    try
    {
        // Use NpgsqlConnectionStringBuilder if present (avoid hard dependency by reflection)
        string host="localhost", db="", user="", pass="", port="5432";
        try {
            var t = Type.GetType("Npgsql.NpgsqlConnectionStringBuilder, Npgsql");
            if(t != null){
                dynamic b = Activator.CreateInstance(t, cs)!;
                host = (string)(b.Host ?? host);
                db   = (string)(b.Database ?? db);
                user = (string)(b.Username ?? user);
                pass = (string)(b.Password ?? pass);
                port = Convert.ToString(b.Port) ?? port;
            }
        } catch {}

        if(string.IsNullOrWhiteSpace(db)){
            await System.IO.File.WriteAllTextAsync(outPath, "-- Could not parse database name. Placeholder.\n", ct);
            return Results.Ok(new { file = fileName, note = "Could not parse DB name (placeholder created)." });
        }

        var psi = new System.Diagnostics.ProcessStartInfo();
        psi.FileName = pgDump!;
        psi.Arguments = $"-h {host} -p {port} -U {user} -F p {db}";
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError  = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow  = true;

        if(!string.IsNullOrWhiteSpace(pass)){
            psi.Environment["PGPASSWORD"] = pass;
        }

        using var proc = System.Diagnostics.Process.Start(psi);
        if(proc == null){
            await System.IO.File.WriteAllTextAsync(outPath, "-- Could not start pg_dump. Placeholder.\n", ct);
            return Results.Ok(new { file = fileName, note = "Could not start pg_dump (placeholder created)." });
        }

        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        proc.WaitForExit();

        if(proc.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout)){
            await System.IO.File.WriteAllTextAsync(outPath, "-- pg_dump failed. Placeholder.\n" + stderr + "\n", ct);
            return Results.Ok(new { file = fileName, note = "pg_dump failed (placeholder created)." });
        }

        await System.IO.File.WriteAllTextAsync(outPath, stdout, ct);
        return Results.Ok(new { file = fileName, note = "pg_dump OK" });
    }
    catch(Exception ex)
    {
        await System.IO.File.WriteAllTextAsync(outPath, "-- Exception running pg_dump. Placeholder.\n" + ex.ToString() + "\n", ct);
        return Results.Ok(new { file = fileName, note = "Exception (placeholder created)." });
    }
});
"@

  # Insert snippet before app.Run / RunAsync (supports app.Run(); await app.RunAsync();)
  $m = [regex]::Match($p, '(?s)\r?\n\s*(?:await\s+)?app\.Run(?:Async)?\s*\(.*?\)\s*;\s*')
  if(!$m.Success){
    # fallback: append at end (should still compile)
    $p2 = $p + "`r`n" + $snippet + "`r`n"
  } else {
    $insertAt = $m.Index
    $p2 = $p.Substring(0,$insertAt) + $snippet + $p.Substring($insertAt)
  }

  Set-Content -Encoding UTF8 -Path $prog -Value $p2
  Write-Host "[OK] Backup API injected into Program.cs"
}

Write-Host ""
Write-Host "DONE. Next commands:"
Write-Host "  cd C:\sami"
Write-Host "  Remove-Item -Recurse -Force .\bin,.\obj -ErrorAction SilentlyContinue"
Write-Host "  dotnet build"
Write-Host "  dotnet run"
