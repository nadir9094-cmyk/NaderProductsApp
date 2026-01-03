using Microsoft.AspNetCore.Mvc;
using NaderProductsApp.Data;

public static class DiagDbApi
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/_diag/db", ([FromServices] AppDbContext db) =>
        {
            var provider = db.Database.ProviderName ?? "";
            var conn = db.Database.GetDbConnection();
            return Results.Ok(new {
                provider,
                dataSource = conn.DataSource,
                database = conn.Database
            });
        });
    }
}
