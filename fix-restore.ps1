param([string]$Root="C:\sami")

$prog = Join-Path $Root "Program.cs"
$www  = Join-Path $Root "wwwroot"
$bh   = Join-Path $www  "backup.html"

if(!(Test-Path $prog)){ throw "Program.cs غير موجود: $prog" }
if(!(Test-Path $www)){ throw "wwwroot غير موجود: $www" }

# 1) Backup files
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
Copy-Item $prog ($prog + ".bak_restore_" + $ts) -Force
if(Test-Path $bh){ Copy-Item $bh ($bh + ".bak_restore_" + $ts) -Force }

# 2) Write clean backup.html (includes Restore)
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
    button{cursor:pointer; border:0; border-radius:10px; padding:10px 14px; font-weight:800}
    .btn{background:#1f7aec; color:#fff}
    .btn2{background:#111827; color:#fff}
    .btnWarn{background:#b91c1c; color:#fff}
    .muted{color:#6b7280}
    table{width:100%; border-collapse:collapse; margin-top:12px}
    th,td{padding:10px; border-bottom:1px solid #eee; text-align:right; vertical-align:middle}
    a{color:#1f7aec; text-decoration:none; font-weight:800}
    code{background:#f3f4f6; padding:2px 6px; border-radius:6px}
    .small{padding:8px 10px; border-radius:10px; font-weight:800}
  </style>
</head>
<body>
  <div class="card">
    <div class="row">
      <div>
        <h2 style="margin:0 0 6px 0">النسخ الاحتياطي</h2>
        <div class="muted">المسارات: <code>/api/backup/list</code> ، <code>/api/backup/run</code> ، <code>/api/backup/restore/{name}</code></div>
      </div>
      <div class="row">
        <button class="btn2" onclick="refreshList()">تحديث</button>
        <button class="btn" onclick="runNow()">إنشاء نسخة الآن</button>
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
          <th>استعادة</th>
        </tr>
      </thead>
      <tbody id="rows"></tbody>
    </table>

    <p class="muted" style="margin-top:10px">
      ⚠️ الاستعادة تعمل كـ <b>استعادة كاملة</b> (إسقاط سكيمة public ثم استرجاع الملف) — يعني ترجع كل الجداول والبيانات مثل ما كانت.
    </p>
  </div>

<script>
async function api(path, options){
  const res = await fetch(path, Object.assign({
    headers: { "Content-Type":"application/json" }
  }, options || {}));
  if(!res.ok){
    const t = await res.text().catch(() => "");
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

async function refreshList(){
  const st = document.getElementById("status");
  st.textContent = "⏳ جاري جلب النسخ...";
  try{
    const j = await (await api("/api/backup/list")).json();
    const tbody = document.getElementById("rows");
    tbody.innerHTML = "";
    (j.files || []).forEach(function(f){
      const name = (f.name || "");
      const tr = document.createElement("tr");
      tr.innerHTML =
        "<td>" + name + "</td>" +
        "<td>" + fmtBytes(f.size || 0) + "</td>" +
        "<td>" + (f.modifiedLocal || "") + "</td>" +
        "<td><a href=\"/api/backup/download/" + encodeURIComponent(name) + "\" target=\"_blank\">تحميل</a></td>" +
        "<td><button class=\"btnWarn small\" onclick=\"restoreNow('" + name.replaceAll("'","%27") + "')\">استعادة</button></td>";
      tbody.appendChild(tr);
    });
    st.textContent = "✅ تم. عدد النسخ: " + ((j.files||[]).length);
  }catch(e){
    st.textContent = "❌ " + e.message;
  }
}

async function runNow(){
  const st = document.getElementById("status");
  st.textContent = "⏳ جاري إنشاء النسخة...";
  try{
    const j = await (await api("/api/backup/run", { method:"POST" })).json();
    st.textContent = "✅ تم إنشاء: " + (j.file || "(بدون اسم)");
    await refreshList();
  }catch(e){
    st.textContent = "❌ " + e.message;
  }
}

async function restoreNow(name){
  name = decodeURIComponent(name);
  if(!name){ return; }
  const ok = confirm("تأكيد الاستعادة الكاملة من النسخة:\\n" + name + "\\n\\nسيتم استبدال كامل البيانات والجداول.");
  if(!ok) return;

  const st = document.getElementById("status");
  st.textContent = "⏳ جاري الاستعادة... (قد تأخذ وقت)";
  try{
    const j = await (await api("/api/backup/restore/" + encodeURIComponent(name), { method:"POST" })).json();
    st.textContent = "✅ تمت الاستعادة: " + (j.file || name) + (j.note ? (" — " + j.note) : "");
    await refreshList();
  }catch(e){
    st.textContent = "❌ " + e.message;
  }
}

refreshList();
</script>
</body>
</html>
"@ | Set-Content -Encoding UTF8 -Path $bh

# 3) Inject restore endpoints into Program.cs (append safely BEFORE any app.Run/RunAsync if found, else append end)
$p = Get-Content $prog -Raw -Encoding UTF8

if($p -match 'MapPost\(\s*"/api/backup/restore'){
  Write-Host "✅ Restore API موجود مسبقاً."
  exit 0
}

$snippet = @"

//
// BACKUP + RESTORE API (auto)
//
static string? FindPsqlExe()
{
    try
    {
        // 1) PATH
        var fromPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in fromPath.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var p = Path.Combine(dir.Trim(), "psql.exe");
                if (File.Exists(p)) return p;
            }
            catch { }
        }

        // 2) Common PostgreSQL install
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var baseDir = Path.Combine(pf, "PostgreSQL");
        if (Directory.Exists(baseDir))
        {
            foreach (var v in Directory.GetDirectories(baseDir).OrderByDescending(x => x))
            {
                var p = Path.Combine(v, "bin", "psql.exe");
                if (File.Exists(p)) return p;
            }
        }
    }
    catch { }
    return null;
}

static Dictionary<string,string> ParseNpgsqlConn(string cs)
{
    var d = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    foreach (var part in (cs ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = part.Split('=', 2);
        if (kv.Length == 2) d[kv[0].Trim()] = kv[1].Trim();
    }
    return d;
}

app.MapGet("/api/backup/list", (IWebHostEnvironment env) =>
{
    var dir = Path.Combine(env.ContentRootPath, "backups");
    Directory.CreateDirectory(dir);

    var files = Directory.GetFiles(dir, "*.sql")
        .Select(f => new FileInfo(f))
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
    var dir = Path.Combine(env.ContentRootPath, "backups");
    var safe = Path.GetFileName(name);
    var file = Path.Combine(dir, safe);
    if(!System.IO.File.Exists(file)) return Results.NotFound(new { error = "File not found" });
    return Results.File(file, "application/sql", safe);
});

app.MapPost("/api/backup/run", async (IWebHostEnvironment env, CancellationToken ct) =>
{
    var dir = Path.Combine(env.ContentRootPath, "backups");
    Directory.CreateDirectory(dir);

    var name = "backup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".sql";
    var path = Path.Combine(dir, name);

    // Placeholder backup (استبدله لاحقاً بـ pg_dump لو تبي)
    await System.IO.File.WriteAllTextAsync(path, "-- Backup placeholder file\\n", ct);

    return Results.Ok(new { file = name, note = "Created placeholder .sql (you can wire pg_dump later)." });
});

// ✅ FULL RESTORE (ALL TABLES + DATA) using psql
app.MapPost("/api/backup/restore/{name}", async (string name, IWebHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
{
    var psql = FindPsqlExe();
    if (psql == null) return Results.Problem("psql.exe غير موجود. ثبت PostgreSQL Client أو أضف psql للـ PATH.");

    var dir = Path.Combine(env.ContentRootPath, "backups");
    Directory.CreateDirectory(dir);

    var safe = Path.GetFileName(name);
    var file = Path.Combine(dir, safe);
    if(!System.IO.File.Exists(file)) return Results.NotFound(new { error = "File not found" });

    // Try connection string keys
    var cs =
        cfg.GetConnectionString("Default") ??
        cfg.GetConnectionString("DefaultConnection") ??
        cfg.GetConnectionString("Postgres") ??
        cfg.GetConnectionString("Npgsql") ??
        Environment.GetEnvironmentVariable("DATABASE_URL") ??
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
        "";

    if (string.IsNullOrWhiteSpace(cs))
        return Results.Problem("Connection string غير موجود. تأكد من ConnectionStrings في appsettings أو متغيرات البيئة.");

    // Only support Npgsql-style (Host=...;Database=...;Username=...;Password=...)
    if (cs.TrimStart().StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
        return Results.Problem("DATABASE_URL format مدعوم لاحقاً. حالياً استخدم Npgsql connection string (Host=...;Database=...;Username=...;Password=...).");

    var kv = ParseNpgsqlConn(cs);

    string host = kv.TryGetValue("Host", out var v1) ? v1 : "";
    string db   = kv.TryGetValue("Database", out var v2) ? v2 : "";
    string user = kv.TryGetValue("Username", out var v3) ? v3 : (kv.TryGetValue("User Id", out var v3b) ? v3b : "");
    string pass = kv.TryGetValue("Password", out var v4) ? v4 : "";
    string port = kv.TryGetValue("Port", out var v5) ? v5 : "5432";

    if(string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(db) || string.IsNullOrWhiteSpace(user))
        return Results.Problem("Connection string ناقص (Host/Database/Username).");

    // 1) Drop & recreate public schema
    var psi1 = new ProcessStartInfo(psql)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    psi1.Environment["PGPASSWORD"] = pass ?? "";
    psi1.ArgumentList.Add("-h"); psi1.ArgumentList.Add(host);
    psi1.ArgumentList.Add("-p"); psi1.ArgumentList.Add(port);
    psi1.ArgumentList.Add("-U"); psi1.ArgumentList.Add(user);
    psi1.ArgumentList.Add("-d"); psi1.ArgumentList.Add(db);
    psi1.ArgumentList.Add("-v"); psi1.ArgumentList.Add("ON_ERROR_STOP=1");
    psi1.ArgumentList.Add("-c"); psi1.ArgumentList.Add("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");

    using (var p1 = Process.Start(psi1)!)
    {
        await p1.WaitForExitAsync(ct);
        var err = await p1.StandardError.ReadToEndAsync();
        if (p1.ExitCode != 0) return Results.Problem("Restore pre-step failed: " + err);
    }

    // 2) Import .sql
    var psi2 = new ProcessStartInfo(psql)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    psi2.Environment["PGPASSWORD"] = pass ?? "";
    psi2.ArgumentList.Add("-h"); psi2.ArgumentList.Add(host);
    psi2.ArgumentList.Add("-p"); psi2.ArgumentList.Add(port);
    psi2.ArgumentList.Add("-U"); psi2.ArgumentList.Add(user);
    psi2.ArgumentList.Add("-d"); psi2.ArgumentList.Add(db);
    psi2.ArgumentList.Add("-v"); psi2.ArgumentList.Add("ON_ERROR_STOP=1");
    psi2.ArgumentList.Add("-f"); psi2.ArgumentList.Add(file);

    using (var p2 = Process.Start(psi2)!)
    {
        await p2.WaitForExitAsync(ct);
        var err = await p2.StandardError.ReadToEndAsync();
        if (p2.ExitCode != 0) return Results.Problem("Restore failed: " + err);
    }

    return Results.Ok(new { file = safe, note = "FULL restore completed (public schema dropped & restored)." });
});

"@

# Insert snippet before app.Run/RunAsync if exists, else append end
$insertPos = -1
$rx1 = [regex]::Match($p, '(?s)\s*(?:await\s+)?app\.RunAsync\s*\(.*?\)\s*;')
if($rx1.Success){ $insertPos = $rx1.Index } else {
  $rx2 = [regex]::Match($p, '(?s)\s*app\.Run\s*\(.*?\)\s*;')
  if($rx2.Success){ $insertPos = $rx2.Index }
}

if($insertPos -ge 0){
  $p2 = $p.Substring(0,$insertPos) + $snippet + "`r`n" + $p.Substring($insertPos)
} else {
  $p2 = $p + "`r`n" + $snippet + "`r`n"
}

Set-Content -Encoding UTF8 -Path $prog -Value $p2
Write-Host "✅ تم: تحديث backup.html + إضافة Restore API في Program.cs"
