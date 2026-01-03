using Microsoft.AspNetCore.Mvc;

public static class ShiftsOpenApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/shifts/open", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            var r = ShiftsStateV1.Open();
            return Results.Ok(r);
        });

        app.MapPost("/api/shifts/close", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            var r = ShiftsStateV1.Close();
            return Results.Ok(r);
        });
    }
}
