$ErrorActionPreference='Stop'
$root='C:\sami'
$www=Join-Path $root 'wwwroot'
$backupsDir=Join-Path $www 'backups'
New-Item -ItemType Directory -Force -Path $www | Out-Null
New-Item -ItemType Directory -Force -Path $backupsDir | Out-Null

# 0) backup.html (اكتبها دائماً)
$backupHtmlPath=Join-Path $www 'backup.html'
$backupHtml=@'
<!doctype html>
<html lang=""ar"" dir=""rtl"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>النسخ الاحتياطي</title>
  <style>
    body{font-family:Tahoma,Arial;background:#f5f6fa;margin:0;padding:24px}
    .card{max-width:920px;margin:auto;background:#fff;border-radius:14px;padding:18px;box-shadow:0 10px 30px rgba(0,0,0,.08)}
    button{border:0;border-radius:10px;padding:10px 14px;cursor:pointer}
    .primary{background:#2d98da;color:#fff}
    a{color:#2d98da;text-decoration:none}
    .muted{color:#556;font-size:14px}
    table{width:100%;border-collapse:collapse;margin-top:12px}
    th,td{padding:10px;border-bottom:1px solid #eef1f5;text-align:right;font-size:14px}
  </style>
</head>
<body>
  <div class=""card"">
    <h2>💾 النسخ الاحتياطي للقاعدة</h2>
    <div class=""muted"" id=""status"">جاهز.</div>
    <p>
      <button class=""primary"" onclick=""runNow()"">📦 إنشاء نسخة الآن</button>
      <button onclick=""refresh()"">🔄 تحديث</button>
    </p>
    <p class=""muted"">روابط API: <a href=""/api/backup/list"">/api/backup/list</a></p>
    <table>
      <thead><tr><th>الملف</th><th>الحجم</th><th>التاريخ</th><th>تحميل</th></tr></thead>
      <tbody id=""rows""></tbody>
    </table>
  </div>
<script>
function fmtBytes(b){ if(b==null) return ''; const u=['B','KB','MB','GB']; let i=0; while(b>=1024&&i<u.length-1){b/=1024;i++;} return (i===0?b:b.toFixed(1))+' '+u[i]; }
async function api(url,opt){ const r=await fetch(url,opt); if(!r.ok) throw new Error(await r.text()); return r; }
async function refresh(){
  const st=document.getElementById('status'); st.textContent='⏳ جاري جلب النسخ...';
  try{
    const j=await (await api('/api/backup/list')).json();
    const tbody=document.getElementById('rows'); tbody.innerHTML='';
    (j.files||[]).forEach(f=>{
      const tr=document.createElement('tr');
      tr.innerHTML = <td></td><td></td><td></td><td><a href="/api/backup/download/">تحميل</a></td>;
      tbody.appendChild(tr);
    });
    st.textContent='✅ تم. عدد النسخ: '+((j.files||[]).length);
  }catch(e){ st.textContent='❌ '+e.message; }
}
async function runNow(){
  const st=document.getElementById('status'); st.textContent='⏳ جاري إنشاء النسخة...';
  try{
    const j=await (await api('/api/backup/run',{method:'POST'})).json();
    st.textContent='✅ تم إنشاء: '+j.file;
    await refresh();
  }catch(e){ st.textContent='❌ '+e.message; }
}
refresh();
</script>
</body>
</html>
'@
Set-Content -Encoding UTF8 -Path $backupHtmlPath -Value $backupHtml

# 1) appsettings.json: BackupSettings (حقن مضمون)
$cfgPath=Join-Path $root 'appsettings.json'
if(!(Test-Path $cfgPath)){ throw 'appsettings.json غير موجود في C:\sami' }
$cfgObj=(Get-Content $cfgPath -Raw) | ConvertFrom-Json
if($null -eq $cfgObj.BackupSettings){ $cfgObj | Add-Member -NotePropertyName BackupSettings -NotePropertyValue ([pscustomobject]@{}) -Force }
if($cfgObj.BackupSettings -isnot [psobject]){ $cfgObj.BackupSettings = [pscustomobject]@{} }

function Set-Prop([object]$o,[string]$n,$v){
  if($o.PSObject.Properties.Name -contains $n){ $o.$n = $v } else { $o | Add-Member -NotePropertyName $n -NotePropertyValue $v -Force }
}
Set-Prop $cfgObj.BackupSettings 'Enabled' $true
Set-Prop $cfgObj.BackupSettings 'TimeLocal' '02:00'
Set-Prop $cfgObj.BackupSettings 'RetainDays' 14
Set-Prop $cfgObj.BackupSettings 'UseDocker' $true
Set-Prop $cfgObj.BackupSettings 'DockerContainerName' 'naderpg'
Set-Prop $cfgObj.BackupSettings 'DatabaseName' 'naderposdb'
Set-Prop $cfgObj.BackupSettings 'DbUser' 'postgres'
Set-Prop $cfgObj.BackupSettings 'OutputDir' 'wwwroot/backups'
($cfgObj | ConvertTo-Json -Depth 50) | Set-Content -Encoding UTF8 -Path $cfgPath

# 2) Program.cs: API + خدمة يومية
$progPath=Join-Path $root 'Program.cs'
if(!(Test-Path $progPath)){ throw 'Program.cs غير موجود في C:\sami' }
$prog=Get-Content $progPath -Raw

$prog=[regex]::Replace($prog,'(?s)//\s*===================== BACKUP_FEATURE_V1\s*=====================.*?//\s*================== END BACKUP_FEATURE_V1\s*==================\s*','')

foreach($u in @('using System.Diagnostics;','using Microsoft.Extensions.Options;')){
  if($prog -notmatch [regex]::Escape($u)){ $prog = $u + ""
"" + $prog }
}

if($prog -notmatch 'Configure<BackupSettings>\('){
  $prog = $prog -replace '(var\s+builder\s*=\s*WebApplication\.CreateBuilder\(args\)\s*;\s*)', ""$1
builder.Services.Configure<BackupSettings>(builder.Configuration.GetSection(""BackupSettings""));
builder.Services.AddHostedService<DailyBackupService>();
""
}

if($prog -match 'app\.Run\(\)\s*;' -and $prog -notmatch '/api/backup/list'){
$endpoints=@'
//
// ===================== BACKUP_FEATURE_V1 =====================
app.MapGet("/api/backup/list", (IOptions<BackupSettings> opt) =>
{
    var s = opt.Value ?? new BackupSettings();
    var dir = Path.Combine(AppContext.BaseDirectory, s.OutputDir ?? "wwwroot/backups");
    Directory.CreateDirectory(dir);

    var files = new DirectoryInfo(dir).GetFiles("*.sql")
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .Select(f => new { name = f.Name, size = f.Length, modifiedLocal = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm") })
        .ToList();

    return Results.Json(new { settings = new { enabled = s.Enabled, timeLocal = s.TimeLocal, retainDays = s.RetainDays }, files });
});

app.MapGet("/api/backup/download/{name}", (string name, IOptions<BackupSettings> opt) =>
{
    var s = opt.Value ?? new BackupSettings();
    var dir = Path.Combine(AppContext.BaseDirectory, s.OutputDir ?? "wwwroot/backups");
    var path = Path.Combine(dir, name);
    if (!System.IO.File.Exists(path)) return Results.NotFound("File not found");
    return Results.File(System.IO.File.ReadAllBytes(path), "application/sql", name);
});

app.MapPost("/api/backup/run", async (IOptions<BackupSettings> opt) =>
{
    var s = opt.Value ?? new BackupSettings();
    var file = await BackupRunner.RunAsync(s);
    return Results.Json(new { ok = true, file });
});
// =================== END BACKUP_FEATURE_V1 ===================
//
'@
  $prog = $prog -replace '(app\.Run\(\)\s*;\s*)', ($endpoints + ""
$1"")
}

if($prog -notmatch 'class\s+BackupSettings'){
$classes=@'

public sealed class BackupSettings
{
    public bool Enabled { get; set; } = true;
    public string TimeLocal { get; set; } = "02:00";
    public int RetainDays { get; set; } = 14;

    public bool UseDocker { get; set; } = true;
    public string DockerContainerName { get; set; } = "naderpg";
    public string DatabaseName { get; set; } = "naderposdb";
    public string DbUser { get; set; } = "postgres";
    public string OutputDir { get; set; } = "wwwroot/backups";
}

public static class BackupRunner
{
    public static async Task<string> RunAsync(BackupSettings s, CancellationToken ct = default)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, s.OutputDir ?? "wwwroot/backups");
        Directory.CreateDirectory(dir);

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, s.RetainDays));
            foreach (var f in new DirectoryInfo(dir).GetFiles("*.sql"))
                if (f.LastWriteTimeUtc < cutoff) f.Delete();
        }
        catch { }

        var file = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
        var path = Path.Combine(dir, file);

        if (!s.UseDocker) throw new InvalidOperationException("Backup requires Docker.");

        var args = $"exec {s.DockerContainerName} pg_dump -U {s.DbUser} -d {s.DatabaseName}";
        await RunProcessToFileAsync("docker", args, path, ct);
        return file;
    }

    private static async Task RunProcessToFileAsync(string exe, string args, string outFile, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {exe}");
        await using var fs = System.IO.File.Create(outFile);
        await p.StandardOutput.BaseStream.CopyToAsync(fs, ct);

        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);

        if (p.ExitCode != 0) throw new InvalidOperationException($"Backup failed: {err}");
    }
}

public sealed class DailyBackupService : BackgroundService
{
    private readonly IOptions<BackupSettings> _opt;
    public DailyBackupService(IOptions<BackupSettings> opt) => _opt = opt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var s = _opt.Value ?? new BackupSettings();
            if (!s.Enabled) { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); continue; }

            var next = GetNextRunUtc(s.TimeLocal);
            var delay = next - DateTime.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.FromSeconds(5);

            await Task.Delay(delay, stoppingToken);
            try { await BackupRunner.RunAsync(s, stoppingToken); } catch { }
        }
    }

    private static DateTime GetNextRunUtc(string timeLocal)
    {
        var parts = (timeLocal ?? "02:00").Split(':');
        var hh = parts.Length>0 && int.TryParse(parts[0], out var a) ? a : 2;
        var mm = parts.Length>1 && int.TryParse(parts[1], out var b) ? b : 0;

        var utcNow = DateTime.UtcNow;
        var ksaNow = utcNow.AddHours(3);
        var nextKsa = new DateTime(ksaNow.Year, ksaNow.Month, ksaNow.Day, hh, mm, 0);
        if (ksaNow >= nextKsa) nextKsa = nextKsa.AddDays(1);
        return nextKsa.AddHours(-3);
    }
}
'@
  $prog = $prog + ""
"" + $classes
}

Set-Content -Encoding UTF8 -Path $progPath -Value $prog
Write-Host '✅ تم تركيب النسخ الاحتياطي بالكامل'
Write-Host 'افتح بعد التشغيل: http://127.0.0.1:5050/backup.html'