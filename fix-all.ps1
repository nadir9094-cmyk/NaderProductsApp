# fix-all.ps1
# ✅ Fixes:
# 1) backup.html JS syntax + ensures correct API URLs
# 2) Adds Backup endpoints to Program.cs if missing
# 3) Ensures BackupSettings has Enabled/TimeLocal/RetainDays (in BackupFeature.cs)
# 4) Fix Npgsql DateTime Kind error by enabling legacy timestamp behavior (quick, safe fix for existing schema)
# 5) Disables duplicate backup feature folders that were extracted inside the project root

param(
  [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Backup-File([string]$path){
  if(Test-Path $path){
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    Copy-Item $path "$path.bak_$stamp" -Force
    Write-Host "🧾 Backup: $path.bak_$stamp"
  }
}

Write-Host "📌 Root:" $Root

# 0) Disable extracted folders that accidentally include duplicate .cs (common cause of CS0101/CS8803)
Get-ChildItem -Path $Root -Directory -ErrorAction SilentlyContinue | Where-Object {
  $_.Name -match 'Nader_BackupAPI_Fix|BackupAPI|_fix|FixPack|NaderFixPack'
} | ForEach-Object {
  $new = $_.FullName + ".DISABLED_" + (Get-Date -Format "yyyyMMdd_HHmmss")
  Rename-Item $_.FullName $new -Force
  Write-Host "🧯 Disabled folder:" $new
}

# Paths
$progPath = Join-Path $Root "Program.cs"
$backupHtml = Join-Path $Root "wwwroot\backup.html"
$backupFeature = Join-Path $Root "BackupFeature.cs"

if(!(Test-Path $progPath)){ throw "❌ Program.cs not found at: $progPath" }
if(!(Test-Path (Split-Path $backupHtml))){ New-Item -ItemType Directory -Force -Path (Split-Path $backupHtml) | Out-Null }

Backup-File $progPath
Backup-File $backupHtml
if(Test-Path $backupFeature){ Backup-File $backupFeature }

# 1) Rewrite backup.html بالكامل (مضمون بدون أخطاء JS)
$backupHtmlContent = @'
<!doctype html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>النسخ الاحتياطي</title>
  <style>
    body{font-family:system-ui,Segoe UI,Tahoma,Arial; margin:16px; background:#f6f7fb}
    .card{background:#fff; border:1px solid #e6e7ef; border-radius:14px; padding:14px; box-shadow:0 6px 18px rgba(0,0,0,.05)}
    .row{display:flex; gap:10px; flex-wrap:wrap; align-items:center}
    button{border:0; background:#2563eb; color:#fff; padding:10px 12px; border-radius:10px; cursor:pointer}
    button.secondary{background:#111827}
    button:disabled{opacity:.6; cursor:not-allowed}
    table{width:100%; border-collapse:collapse; margin-top:10px}
    th,td{border-bottom:1px solid #eee; padding:10px; text-align:right; font-size:14px}
    th{color:#374151; background:#fafafa}
    #status{margin-top:10px; font-size:14px}
    .muted{color:#6b7280}
    a{color:#2563eb; text-decoration:none}
  </style>
</head>
<body>
  <div class="card">
    <div class="row" style="justify-content:space-between">
      <div>
        <div style="font-size:18px;font-weight:700">النسخ الاحتياطي</div>
        <div class="muted">عرض النسخ + إنشاء نسخة الآن</div>
      </div>
      <div class="row">
        <button id="btnRefresh" class="secondary">تحديث</button>
        <button id="btnRun">إنشاء نسخة الآن</button>
      </div>
    </div>

    <div id="status" class="muted">جاهز.</div>

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
const API_BASE = ""; // نفس الدومين (http://127.0.0.1:5050)
async function api(path, options){
  const res = await fetch(API_BASE + path, { headers: { "Content-Type":"application/json" }, ...(options||{}) });
  if(!res.ok){
    const t = await res.text().catch(()=> "");
    throw new Error(res.status + " " + (t||res.statusText));
  }
  return res;
}

function fmtSize(n){
  n = Number(n||0);
  if(n < 1024) return n + " B";
  if(n < 1024*1024) return (n/1024).toFixed(1) + " KB";
  return (n/1024/1024).toFixed(1) + " MB";
}

async function refresh(){
  const st = document.getElementById("status");
  st.textContent = "⏳ جاري جلب النسخ...";
  const tbody = document.getElementById("rows");
  tbody.innerHTML = "";
  try{
    const j = await (await api("/api/backup/list")).json();
    (j.files || []).forEach(f=>{
      const tr = document.createElement("tr");
      const name = f.name || "";
      const size = fmtSize(f.size || 0);
      const mod = f.modifiedLocal || f.modifiedUtc || "";
      tr.innerHTML = `
        <td>${name}</td>
        <td>${size}</td>
        <td>${mod}</td>
        <td><a href="/api/backup/download/${encodeURIComponent(name)}">تحميل</a></td>
      `;
      tbody.appendChild(tr);
    });
    st.textContent = "✅ تم. عدد النسخ: " + ((j.files||[]).length);
  }catch(e){
    st.textContent = "❌ " + e.message;
  }
}

async function runNow(){
  const st = document.getElementById("status");
  const btn = document.getElementById("btnRun");
  btn.disabled = true;
  st.textContent = "⏳ جاري إنشاء النسخة...";
  try{
    const j = await (await api("/api/backup/run",{method:"POST"})).json();
    st.textContent = "✅ تم إنشاء: " + (j.file || "");
    await refresh();
  }catch(e){
    st.textContent = "❌ " + e.message;
  }finally{
    btn.disabled = false;
  }
}

document.getElementById("btnRefresh").addEventListener("click", refresh);
document.getElementById("btnRun").addEventListener("click", runNow);
refresh();
</script>
</body>
</html>
'@

Set-Content -Encoding UTF8 -Path $backupHtml -Value $backupHtmlContent
Write-Host "✅ backup.html تم استبداله بنسخة سليمة."

# 2) Ensure BackupSettings has Enabled/TimeLocal/RetainDays in BackupFeature.cs (if the project uses it)
if(Test-Path $backupFeature){
  $t = Get-Content $backupFeature -Raw -Encoding UTF8
  if($t -match 'class\s+BackupSettings'){
    # insert properties just after class opening brace if missing
    if(($t -notmatch '\bEnabled\b') -or ($t -notmatch '\bTimeLocal\b') -or ($t -notmatch '\bRetainDays\b')){
      $t2 = [regex]::Replace($t, '(?s)(public\s+sealed\s+class\s+BackupSettings\s*\{)', '$1
    // ✅ Added by fix-all.ps1 (required by Program.cs)
    public bool Enabled { get; set; } = true;
    // مثال: "02:00" (توقيت محلي)
    public string? TimeLocal { get; set; } = "02:00";
    // عدد الأيام للاحتفاظ بالنسخ
    public int RetainDays { get; set; } = 30;
', 1)
      Set-Content -Encoding UTF8 -Path $backupFeature -Value $t2
      Write-Host "✅ BackupFeature.cs: Added Enabled/TimeLocal/RetainDays to BackupSettings."
    } else {
      Write-Host "ℹ️ BackupFeature.cs: BackupSettings already has Enabled/TimeLocal/RetainDays."
    }
  } else {
    Write-Host "ℹ️ BackupFeature.cs موجود لكن ما لقينا BackupSettings داخله."
  }
} else {
  Write-Host "ℹ️ BackupFeature.cs غير موجود (بنضيف الـ APIs داخل Program.cs فقط)."
}

# 3) Patch Program.cs:
# 3a) Enable legacy timestamp behavior to avoid DateTimeKind Unspecified -> timestamptz crash (Expenses وغيرها)
$prog = Get-Content $progPath -Raw -Encoding UTF8
if($prog -notmatch 'Npgsql\.EnableLegacyTimestampBehavior'){
  # place it near the top, before builder
  $prog = [regex]::Replace($prog, '(?m)^\s*var\s+builder\s*=\s*WebApplication\.CreateBuilder\(args\);\s*$', @'
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true); // ✅ Fix DateTime Kind issues with timestamptz
var builder = WebApplication.CreateBuilder(args);
'@, 1)
  Write-Host "✅ Program.cs: Added Npgsql legacy timestamp switch."
} else {
  Write-Host "ℹ️ Program.cs: Legacy timestamp switch already present."
}

# 3b) Ensure backup endpoints exist; if missing, inject a minimal block before app.Run()
$needList = ($prog -notmatch 'MapGet\(\s*"/api/backup/list"')
$needRun  = ($prog -notmatch 'MapPost\(\s*"/api/backup/run"')
$needDl   = ($prog -notmatch 'MapGet\(\s*"/api/backup/download/\{name\}"')

if($needList -or $needRun -or $needDl){
  $inject = @'
//
// ===== BACKUP API (added by fix-all.ps1) =====
//
app.MapGet("/api/backup/list", (IHostEnvironment env) =>
{
    var dir = Path.Combine(env.ContentRootPath, "App_Data", "Backups");
    Directory.CreateDirectory(dir);
    var files = Directory.GetFiles(dir, "*.sql")
        .Select(p => new FileInfo(p))
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .Select(f => new {
            name = f.Name,
            size = f.Length,
            modifiedUtc = f.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            modifiedLocal = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        })
        .ToList();
    return Results.Ok(new { files });
});

app.MapGet("/api/backup/download/{name}", (string name, IHostEnvironment env) =>
{
    var dir = Path.Combine(env.ContentRootPath, "App_Data", "Backups");
    var path = Path.Combine(dir, name);
    if(!System.IO.File.Exists(path)) return Results.NotFound();
    return Results.File(path, "application/sql", name);
});

app.MapPost("/api/backup/run", async (IConfiguration cfg, IHostEnvironment env) =>
{
    // يعتمد على pg_dump إذا كان موجوداً في PATH.
    // إذا ما عندك pg_dump، يطلع خطأ واضح في الاستجابة.
    var dir = Path.Combine(env.ContentRootPath, "App_Data", "Backups");
    Directory.CreateDirectory(dir);

    var cs = cfg.GetConnectionString("DefaultConnection") ?? cfg.GetConnectionString("Default") ?? "";
    if(string.IsNullOrWhiteSpace(cs)) return Results.BadRequest(new { error="No connection string found (DefaultConnection)." });

    // استخراج قيم مهمة من ConnectionString
    var b = new Npgsql.NpgsqlConnectionStringBuilder(cs);
    var host = b.Host;
    var port = b.Port;
    var database = b.Database;
    var username = b.Username;
    var password = b.Password;

    var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    var outFile = Path.Combine(dir, $"backup_{stamp}.sql");

    var psi = new System.Diagnostics.ProcessStartInfo("pg_dump")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    psi.ArgumentList.Add("-h"); psi.ArgumentList.Add(host);
    psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(port.ToString());
    psi.ArgumentList.Add("-U"); psi.ArgumentList.Add(username);
    psi.ArgumentList.Add("-F"); psi.ArgumentList.Add("p");
    psi.ArgumentList.Add(database);

    psi.Environment["PGPASSWORD"] = password;

    using var p = System.Diagnostics.Process.Start(psi);
    if(p is null) return Results.Problem("Failed to start pg_dump.");
    var stdout = await p.StandardOutput.ReadToEndAsync();
    var stderr = await p.StandardError.ReadToEndAsync();
    await p.WaitForExitAsync();

    if(p.ExitCode != 0)
        return Results.Problem($"pg_dump failed (exit {p.ExitCode}): {stderr}");

    await System.IO.File.WriteAllTextAsync(outFile, stdout, System.Text.Encoding.UTF8);
    return Results.Ok(new { file = Path.GetFileName(outFile) });
});
//
// ===== END BACKUP API =====
//
'@

  # inject before the last app.Run or app.RunAsync
  if($prog -match '(?s)(.*)\bapp\.Run(Async)?\s*\('){
    $prog = [regex]::Replace($prog, '(?s)\R\s*app\.Run(Async)?\s*\(.*?\);\s*', "`r`n$inject`r`n`$0", 1)
    Write-Host "✅ Program.cs: Injected Backup API block."
  } else {
    # fallback: append at end
    $prog = $prog + "`r`n" + $inject
    Write-Host "⚠️ Program.cs: app.Run not found; appended Backup API block at end."
  }
} else {
  Write-Host "ℹ️ Program.cs: Backup endpoints already exist."
}

Set-Content -Encoding UTF8 -Path $progPath -Value $prog

# 4) Clean + Build
Remove-Item -Recurse -Force (Join-Path $Root "bin"),(Join-Path $Root "obj") -ErrorAction SilentlyContinue
Write-Host "🔨 Building..."
dotnet build | Out-Host

Write-Host "`n🎉 تم! شغّل الآن:"
Write-Host "   dotnet run"