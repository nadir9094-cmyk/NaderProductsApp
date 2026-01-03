using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class DiagPatchDbApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/_diag/patch/cashierinvoice-cols", async (AppDbContext db, HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();

            // Postgres only
            var provider = db.Database.ProviderName ?? "";
            if (!provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { ok=false, provider });

            // أضف الأعمدة إذا غير موجودة (Postgres)
            var sql = @"
ALTER TABLE ""CashierInvoices"" ADD COLUMN IF NOT EXISTS ""CashierId"" integer NULL;
ALTER TABLE ""CashierInvoices"" ADD COLUMN IF NOT EXISTS ""CashierName"" text NULL;
";
            await db.Database.ExecuteSqlRawAsync(sql);
            return Results.Ok(new { ok = true, patched = true });
        });
    }
}
