using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class EmployeesApiV1
{
    // 🔐 قراءة التوكن من Authorization Header
    public static string ReadBearerToken(HttpRequest http)
    {
        var h = http.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(h)) return "";
        if (!h.StartsWith("Bearer ")) return "";
        return h.Substring("Bearer ".Length).Trim();
    }

    // 👤 جلب الموظف من التوكن
    public static async Task<NaderProductsApp.Models.Employee?> GetEmployeeFromToken(
        AppDbContext db, HttpRequest http)
    {
        var token = ReadBearerToken(http);
        if (string.IsNullOrWhiteSpace(token)) return null;

        var session = await db.EmployeeSessions
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Token == token && s.ExpiresAtUtc > DateTime.UtcNow);

        return session?.Employee;
    }

    // 📋 API الموظفين (بدون تعقيد)
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
