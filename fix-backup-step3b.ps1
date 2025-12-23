$ErrorActionPreference="Stop"

$progPath = "C:\sami\Program.cs"
if(-not (Test-Path $progPath)){ throw "Program.cs غير موجود" }

Copy-Item $progPath "$progPath.bak_before_step3b" -Force

$prog = Get-Content $progPath -Raw -Encoding UTF8

# 1) احذف كلاسات النسخ الاحتياطي فقط (بدون ما نمس endpoints)
$patterns = @(
  '(?s)\s*public\s+sealed\s+class\s+BackupSettings\b.*?\n}\s*',
  '(?s)\s*public\s+static\s+class\s+BackupRunner\b.*?\n}\s*',
  '(?s)\s*public\s+sealed\s+class\s+DailyBackupService\b.*?\n}\s*'
)

foreach($pat in $patterns){
  $prog = [regex]::Replace($prog, $pat, "`r`n")
}

# 2) تأكد إن Program.cs يحتوي سطر تشغيل للتطبيق
# (لو مو موجود نضيف app.Run(); في النهاية)
if($prog -notmatch 'app\.Run(Async)?\s*\('){
  $prog = $prog.TrimEnd() + "`r`n`r`napp.Run();`r`n"
}

Set-Content -Encoding UTF8 -Path $progPath -Value $prog

# 3) أنشئ ملف BackupFeature.cs للكلاسات
$featurePath = "C:\sami\BackupFeature.cs"

$feature = @"
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class BackupSettings
{
    public bool Enabled { get; set; } = true;
    public string TimeLocal { get; set; } = "02:00"; // 24h KSA
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

        // retention
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
        // KSA = UTC+3 (no DST)
        var parts = (timeLocal ?? "02:00").Split(':');
        var hh = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 2;
        var mm = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;

        var utcNow = DateTime.UtcNow;
        var ksaNow = utcNow.AddHours(3);
        var nextKsa = new DateTime(ksaNow.Year, ksaNow.Month, ksaNow.Day, hh, mm, 0);
        if (ksaNow >= nextKsa) nextKsa = nextKsa.AddDays(1);
        return nextKsa.AddHours(-3);
    }
}
"@

Set-Content -Encoding UTF8 -Path $featurePath -Value $feature

Write-Host "✅ OK: Removed backup classes from Program.cs + created BackupFeature.cs"
Write-Host "✅ Backup of Program.cs: $progPath.bak_before_step3b"
