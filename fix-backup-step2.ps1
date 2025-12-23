$ErrorActionPreference="Stop"

$progPath = "C:\sami\Program.cs"
if(-not (Test-Path $progPath)){ throw "Program.cs غير موجود: $progPath" }

$prog = Get-Content $progPath -Raw -Encoding UTF8

# usings المطلوبة
$needUsings = @(
  'using System.Diagnostics;',
  'using Microsoft.Extensions.Options;',
  'using Microsoft.Extensions.Hosting;'
)
foreach($u in $needUsings){
  if($prog -notmatch [regex]::Escape($u)){
    $prog = $u + "
" + $prog
  }
}

# Services بعد builder
$re = [regex]'var\s+builder\s*=\s*WebApplication\.CreateBuilder\(args\)\s*;\s*'
if(-not $re.IsMatch($prog)){ throw "ما لقيت سطر CreateBuilder(args) في Program.cs" }

if($prog -notmatch 'Configure<BackupSettings>\(' -or $prog -notmatch 'AddHostedService<DailyBackupService>\('){
  $prog = $re.Replace($prog, { param($m)
    $m.Value + "
" +
    'builder.Services.Configure<BackupSettings>(builder.Configuration.GetSection("BackupSettings"));' + "
" +
    'builder.Services.AddHostedService<DailyBackupService>();' + "
"
  }, 1)
}

# Endpoints قبل app.Run()
if($prog -notmatch '/api/backup/list'){
  $endpoints = @'
//
// ===================== BACKUP_FEATURE_V2 =====================
app.MapGet("/api/backup/list", (IOptions<BackupSettings> opt, IHostEnvironment env) =>
{
    var s = opt.Value ?? new BackupSettings();
    var dir = Path.Combine(env.ContentRootPath, s.OutputDir ?? "wwwroot/backups");
    Directory.CreateDirectory(dir);

    var files = new DirectoryInfo(dir).GetFiles("*.sql")
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .Select(f => new { name = f.Name, size = f.Length, modifiedLocal = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm") })
        .ToList();

    return Results.Json(new { settings = new { enabled = s.Enabled, timeLocal = s.TimeLocal, retainDays = s.RetainDays }, files });
});

app.MapGet("/api/backup/download/{name}", (string name, IOptions<BackupSettings> opt, IHostEnvironment env) =>
{
    var s = opt.Value ?? new BackupSettings();
    var dir = Path.Combine(env.ContentRootPath, s.OutputDir ?? "wwwroot/backups");
    var path = Path.Combine(dir, name);
    if (!System.IO.File.Exists(path)) return Results.NotFound("File not found");
    return Results.File(System.IO.File.ReadAllBytes(path), "application/sql", name);
});

app.MapPost("/api/backup/run", async (IOptions<BackupSettings> opt, IHostEnvironment env) =>
{
    var s = opt.Value ?? new BackupSettings();
    var file = await BackupRunner.RunAsync(env.ContentRootPath, s);
    return Results.Json(new { ok = true, file });
});
// =================== END BACKUP_FEATURE_V2 ===================
//
'@

  if($prog -match 'app\.Run\(\)\s*;'){
    $prog = [regex]::Replace($prog, 'app\.Run\(\)\s*;', ($endpoints + "
app.Run();"), 1)
  } else {
    $prog = $prog + "
" + $endpoints
  }
}

# Classes (إذا ناقصة)
if($prog -notmatch 'public\s+sealed\s+class\s+BackupSettings'){
  $classes = @'

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
    public static async Task<string> RunAsync(string contentRootPath, BackupSettings s, CancellationToken ct = default)
    {
        var dir = Path.Combine(contentRootPath, s.OutputDir ?? "wwwroot/backups");
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

        if (!s.UseDocker)
            throw new InvalidOperationException("Backup requires Docker in your setup.");

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

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Backup failed: {err}");
    }
}

public sealed class DailyBackupService : BackgroundService
{
    private readonly IOptions<BackupSettings> _opt;
    private readonly IHostEnvironment _env;

    public DailyBackupService(IOptions<BackupSettings> opt, IHostEnvironment env)
    {
        _opt = opt;
        _env = env;
    }

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

            try { await BackupRunner.RunAsync(_env.ContentRootPath, s, stoppingToken); }
            catch { }
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
  $prog = $prog + "
" + $classes
}

Set-Content -Encoding UTF8 -Path $progPath -Value $prog
Write-Host "✅ OK: Program.cs updated (Backup V2)"
