using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class ShiftsApiV1
{
    // الشفت الحالي (مؤقت)
    public static void MapShiftsApi(WebApplication app)
    {
        app.MapGet("/api/shifts/current", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            return Results.Ok(new { open = false, id = 0 });
        });
    }

    // سجل الشفتات (مؤقت)
    public static void MapShiftListApi(WebApplication app)
    {
        app.MapGet("/api/shifts/list", (HttpRequest http, int days = 60, int take = 200) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            return Results.Ok(new
            {
                total = 0,
                items = Array.Empty<object>()
            });
        });
    }
}
