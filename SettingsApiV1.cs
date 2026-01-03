using Microsoft.AspNetCore.Mvc;

public static class SettingsApiV1
{
    public static void Map(WebApplication app)
    {
        // GET settings (مؤقت)
        app.MapGet("/api/settings", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            // مؤقتاً: رجّع إعدادات افتراضية
            return Results.Ok(new {
                storeName = "NADER POS",
                vatRate = 0.15,
                cashierPaperWidthMm = 80
            });
        });

        // POST settings (مؤقت)
        app.MapPost("/api/settings", async (HttpRequest http, [FromBody] object body) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            // مؤقتاً: اعتبرها محفوظة
            return Results.Ok(new { ok = true });
        });
    }
}
