$ErrorActionPreference="Stop"

# 1) اكتب ملف تشخيص: يوضح هل الـ env vars موجودة داخل Railway وقت التشغيل
@"
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class DiagEnvApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/_diag/env", ([FromServices] AppDbContext db) =>
        {
            string? Get(string k) => Environment.GetEnvironmentVariable(k);

            var keys = new[]
            {
                "DATABASE_URL",
                "DATABASE_PRIVATE_URL",
                "DATABASE_PUBLIC_URL",
                "POSTGRES_URL",
                "POSTGRESQL_URL",
                "RAILWAY_DATABASE_URL",
                "DATABASE_URL_NON_POOLING",
                "ConnectionStrings__Default",
                "CONNECTIONSTRINGS__DEFAULT",
                "PGHOST","PGPORT","PGDATABASE","PGUSER"
            };

            var map = keys.ToDictionary(k => k, k => Get(k));

            return Results.Ok(new
            {
                provider = db.Database.ProviderName,
                env = map
            });
        });
    }
}
"@ | Set-Content -Encoding UTF8 .\DiagEnvApiV1.cs

# 2) اربطها في Program.cs مرة واحدة (جنب ربط Auth)
$path=".\Program.cs"
$txt = Get-Content $path -Raw -Encoding UTF8

if ($txt -notmatch "DiagEnvApiV1\.Map\(app\);")
{
    $txt = $txt -replace 'AuthApiV1\.MapAuthApi\(app\);\s*', "AuthApiV1.MapAuthApi(app);`nDiagEnvApiV1.Map(app);`n"
    Set-Content -Encoding UTF8 -Path $path -Value $txt
}

dotnet build | Out-Host
"OK: DiagEnvApiV1 added. Now commit & push then redeploy on Railway." 
