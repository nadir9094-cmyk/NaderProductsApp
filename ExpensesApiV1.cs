public static class ExpensesApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/expenses", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            // مؤقتاً: مصروفات فاضية (لين نربط موديل المصروفات)
            return Results.Ok(System.Array.Empty<object>());
        });
    }
}
