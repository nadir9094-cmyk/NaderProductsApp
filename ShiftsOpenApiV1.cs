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

            return Results.Ok(ShiftsStateV1.Open("cashier"));
        });

        app.MapPost("/api/shifts/close", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            return Results.Ok(ShiftsStateV1.Close());
        });
    }
}
