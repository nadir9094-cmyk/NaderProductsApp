using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

public static class BackupApiV2
{
    public static void MapBackupApiV2(this WebApplication app)
    {
        var g = app.MapGroup("/api/backup2");

        g.MapGet("/list", (IWebHostEnvironment env) =>
        {
            var dir = Path.Combine(env.ContentRootPath, "backups");
            Directory.CreateDirectory(dir);

            var files = Directory.GetFiles(dir, "*.sql")
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .Select(fi => new
                {
                    name = fi.Name,
                    size = fi.Length,
                    modifiedUtc = fi.LastWriteTimeUtc,
                    modifiedLocal = fi.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToList();

            return Results.Ok(new { files });
        });

        g.MapGet("/download/{name}", (string name, IWebHostEnvironment env) =>
        {
            var dir = Path.Combine(env.ContentRootPath, "backups");
            var safe = Path.GetFileName(name);
            var file = Path.Combine(dir, safe);
            if (!System.IO.File.Exists(file)) return Results.NotFound(new { error = "File not found" });
            return Results.File(file, "application/sql", safe);
        });

        g.MapPost("/run", async (IWebHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
        {
            var dir = Path.Combine(env.ContentRootPath, "backups");
            Directory.CreateDirectory(dir);

            var name = "backup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".sql";
            var path = Path.Combine(dir, name);

            // Try real PostgreSQL backup via pg_dump (if available). Otherwise create placeholder.
            var cs = ResolveConnectionString(cfg);
            if (string.IsNullOrWhiteSpace(cs))
            {
                await File.WriteAllTextAsync(path, "-- No connection string found; placeholder backup.\n", ct);
                return Results.Ok(new { file = name, note = "No connection string found. Placeholder created." });
            }

            var b = new Npgsql.NpgsqlConnectionStringBuilder(cs);

            // Prefer pg_dump in PATH
            var pgDumpOk = await TryRunProcess(
                fileName: "pg_dump",
                arguments: $"-h \"{b.Host}\" -p {b.Port} -U \"{b.Username}\" -d \"{b.Database}\" -F p -f \"{path}\"",
                password: b.Password,
                ct: ct);

            if (!pgDumpOk.success)
            {
                await File.WriteAllTextAsync(path, "-- pg_dump not available or failed; placeholder backup.\n", ct);
                return Results.Ok(new { file = name, note = "pg_dump failed; placeholder created.", error = pgDumpOk.error });
            }

            return Results.Ok(new { file = name });
        });

        g.MapPost("/restore/{name}", async (string name, IWebHostEnvironment env, IConfiguration cfg, CancellationToken ct) =>
        {
            var dir = Path.Combine(env.ContentRootPath, "backups");
            var safe = Path.GetFileName(name);
            var file = Path.Combine(dir, safe);
            if (!System.IO.File.Exists(file)) return Results.NotFound(new { error = "File not found" });

            var cs = ResolveConnectionString(cfg);
            if (string.IsNullOrWhiteSpace(cs))
                return Results.Problem("No connection string found. Cannot restore.");

            var b = new Npgsql.NpgsqlConnectionStringBuilder(cs);

            // Full restore by executing SQL file using psql (recommended)
            var psqlOk = await TryRunProcess(
                fileName: "psql",
                arguments: $"-h \"{b.Host}\" -p {b.Port} -U \"{b.Username}\" -d \"{b.Database}\" -v ON_ERROR_STOP=1 -f \"{file}\"",
                password: b.Password,
                ct: ct);

            if (!psqlOk.success)
                return Results.Problem("psql failed: " + (psqlOk.error ?? "unknown error"));

            return Results.Ok(new { message = "Restored from " + safe });
        });
    }

    private static string? ResolveConnectionString(IConfiguration cfg)
    {
        // Try common keys
        string? cs =
            cfg.GetConnectionString("Default") ??
            cfg.GetConnectionString("DefaultConnection") ??
            cfg.GetConnectionString("Postgres") ??
            cfg.GetConnectionString("Nader") ??
            cfg["ConnectionStrings:Default"] ??
            cfg["ConnectionStrings:DefaultConnection"] ??
            cfg["ConnectionStrings:Postgres"] ??
            cfg["DATABASE_URL"] ??
            cfg["ConnectionString"];

        return string.IsNullOrWhiteSpace(cs) ? null : cs;
    }

    private static async Task<(bool success, string? error)> TryRunProcess(string fileName, string arguments, string? password, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(password))
                psi.Environment["PGPASSWORD"] = password;

            using var p = Process.Start(psi);
            if (p == null) return (false, "Process start returned null");

            await p.WaitForExitAsync(ct);

            if (p.ExitCode != 0)
            {
                var err = await p.StandardError.ReadToEndAsync(ct);
                if (string.IsNullOrWhiteSpace(err))
                    err = await p.StandardOutput.ReadToEndAsync(ct);
                return (false, err);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
