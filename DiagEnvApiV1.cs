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
