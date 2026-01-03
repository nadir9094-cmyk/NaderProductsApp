public static class SettingsApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/settings", () =>
        {
            return Results.Ok(new {
                storeName = "Nader POS",
                vatRate = 0.15,
                currency = "SAR"
            });
        });
    }
}
