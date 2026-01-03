using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class EmployeesApiV1
{
    public static void MapEmployeesApi(WebApplication app)
    {
        app.MapGet("/api/employees", async (AppDbContext db) =>
        {
            var emps = await db.Employees
                .Where(e => e.IsActive)
                .Select(e => new {
                    id = e.Id,
                    fullName = e.FullName,
                    username = e.Username,
                    permissions = e.Permissions,
                    isActive = e.IsActive
                })
                .ToListAsync();

            return Results.Ok(emps);
        });
    }
}
