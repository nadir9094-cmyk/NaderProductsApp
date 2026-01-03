using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class CashiersApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/cashiers", async (AppDbContext db, HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            var emps = await db.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .Select(e => new { id = e.Id, name = e.FullName })
                .ToListAsync();

            return Results.Ok(emps);
        });
    }
}
