using Microsoft.AspNetCore.Mvc;

public static class ShiftsApiV1
{
    public static void MapShiftsApi(WebApplication app)
    {
        app.MapGet("/api/shifts/current", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            return Results.Ok(ShiftsStateV1.Current());
        });
    }

    public static void MapShiftListApi(WebApplication app)
    {
        app.MapGet("/api/shifts/list", (HttpRequest http, int days = 60, int take = 200) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            return Results.Ok(ShiftsStateV1.List(days, take));
        });
    }
}
