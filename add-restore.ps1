param([string]$Root="C:\sami")

$prog = Join-Path $Root "Program.cs"
$p = Get-Content $prog -Raw -Encoding UTF8

if($p -match 'api/backup/restore'){
  Write-Host "Restore API already exists"
  exit
}

$snippet = @"

//
// BACKUP RESTORE (FULL DATABASE)
//
app.MapPost("/api/backup/restore/{file}", async (string file, IConfiguration cfg, IWebHostEnvironment env) =>
{
    var backupsDir = Path.Combine(env.ContentRootPath, "backups");
    var safe = Path.GetFileName(file);
    var path = Path.Combine(backupsDir, safe);

    if(!System.IO.File.Exists(path))
        return Results.NotFound(new { error = "Backup file not found" });

    var cs = cfg.GetConnectionString("DefaultConnection");
    var builder = new Npgsql.NpgsqlConnectionStringBuilder(cs);

    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "psql",
        Arguments = $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -d {builder.Database} -f \"{path}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    psi.Environment["PGPASSWORD"] = builder.Password;

    var proc = System.Diagnostics.Process.Start(psi)!;
    await proc.WaitForExitAsync();

    if(proc.ExitCode != 0)
        return Results.Problem(await proc.StandardError.ReadToEndAsync());

    return Results.Ok(new { restored = safe });
});

"@

# insert before app.Run
$idx = $p.LastIndexOf("app.Run", [StringComparison]::Ordinal)
if($idx -lt 0){ throw "app.Run not found" }

$p2 = $p.Substring(0,$idx) + $snippet + "`r`n" + $p.Substring($idx)
Set-Content -Encoding UTF8 -Path $prog -Value $p2

Write-Host "✔ Restore API added"
