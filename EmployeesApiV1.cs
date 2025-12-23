using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Models;

using AppDbContext = NaderProductsApp.Data.AppDbContext;
using NaderProductsApp.Data;
public static class EmployeesApiV1
{
    public static string? ReadBearerToken(HttpRequest http)
    {
        if (!http.Headers.TryGetValue("Authorization", out var v)) return null;
        var s = v.ToString();
        if (s.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return s.Substring(7).Trim();
        return null;
    }

    public static async Task<Employee?> GetEmployeeFromToken(AppDbContext db, HttpRequest http)
    {
        var token = ReadBearerToken(http);
        if (string.IsNullOrWhiteSpace(token)) return null;

        var now = DateTime.UtcNow;
        var session = await db.EmployeeSessions
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Token == token);

        if (session?.Employee is null) return null;
        if (session.ExpiresAtUtc <= now)
        {
            db.EmployeeSessions.Remove(session);
            await db.SaveChangesAsync();
            return null;
        }
        if (!session.Employee.IsActive) return null;

        return session.Employee;
    }

    private static bool HasPerm(Employee emp, EmployeePermissions perm)
    {
        var p = (EmployeePermissions)emp.Permissions;
        return p.HasFlag(EmployeePermissions.All) || p.HasFlag(EmployeePermissions.Admin) || p.HasFlag(perm);
    }

    public static void MapEmployeesApi(WebApplication app)
    {
        // list
        app.MapGet("/api/employees", async (AppDbContext db, HttpRequest http) =>
        {
            var me = await GetEmployeeFromToken(db, http);
            if (me is null || !HasPerm(me, EmployeePermissions.Employees)) return Results.Unauthorized();

            var list = await db.Employees
                .OrderByDescending(x => x.Id)
                .Select(x => new {
                    x.Id, x.FullName, x.Username, x.Permissions, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc
                })
                .ToListAsync();

            return Results.Ok(list);
        });

        // create
        app.MapPost("/api/employees", async (AppDbContext db, HttpRequest http, CreateEmployeeReq req) =>
        {
            var me = await GetEmployeeFromToken(db, http);
            if (me is null || !HasPerm(me, EmployeePermissions.Employees)) return Results.Unauthorized();

            var fullName = (req.FullName ?? "").Trim();
            var username = (req.Username ?? "").Trim().ToLowerInvariant();
            var password = req.Password ?? "";

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || password.Length < 4)
                return Results.BadRequest("INVALID_DATA");

            var exists = await db.Employees.AnyAsync(x => x.Username.ToLower() == username);
            if (exists) return Results.Conflict("USERNAME_EXISTS");

            var (hashB64, saltB64) = HashPassword(password);
            var emp = new Employee
            {
                FullName = fullName,
                Username = username,
                PasswordHash = hashB64,
                PasswordSalt = saltB64,
                Permissions = req.Permissions,
                IsActive = req.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.Employees.Add(emp);
            await db.SaveChangesAsync();

            return Results.Ok(new { emp.Id });
        });

        // update
        app.MapPut("/api/employees/{id:int}", async (AppDbContext db, HttpRequest http, int id, UpdateEmployeeReq req) =>
        {
            var me = await GetEmployeeFromToken(db, http);
            if (me is null || !HasPerm(me, EmployeePermissions.Employees)) return Results.Unauthorized();

            var emp = await db.Employees.FirstOrDefaultAsync(x => x.Id == id);
            if (emp is null) return Results.NotFound();

            var fullName = (req.FullName ?? "").Trim();
            var username = (req.Username ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username))
                return Results.BadRequest("INVALID_DATA");

            var exists = await db.Employees.AnyAsync(x => x.Id != id && x.Username.ToLower() == username);
            if (exists) return Results.Conflict("USERNAME_EXISTS");

            emp.FullName = fullName;
            emp.Username = username;
            emp.Permissions = req.Permissions;
            emp.IsActive = req.IsActive;
            emp.UpdatedAtUtc = DateTime.UtcNow;

            // change password (optional)
            if (!string.IsNullOrWhiteSpace(req.NewPassword))
            {
                if (req.NewPassword!.Length < 4) return Results.BadRequest("WEAK_PASSWORD");
                var (h, s) = HashPassword(req.NewPassword!);
                emp.PasswordHash = h;
                emp.PasswordSalt = s;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // delete
        app.MapDelete("/api/employees/{id:int}", async (AppDbContext db, HttpRequest http, int id) =>
        {
            var me = await GetEmployeeFromToken(db, http);
            if (me is null || !HasPerm(me, EmployeePermissions.Employees)) return Results.Unauthorized();

            var emp = await db.Employees.FirstOrDefaultAsync(x => x.Id == id);
            if (emp is null) return Results.NotFound();

            // منع حذف آخر مدير
            if (((EmployeePermissions)emp.Permissions).HasFlag(EmployeePermissions.All) && await db.Employees.CountAsync() == 1)
                return Results.BadRequest("CANNOT_DELETE_LAST_ADMIN");

            db.Employees.Remove(emp);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static (string hashB64, string saltB64) HashPassword(string password)
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var salt = new byte[16];
        rng.GetBytes(salt);
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 120_000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public record CreateEmployeeReq(string FullName, string Username, string Password, long Permissions, bool IsActive);
    public record UpdateEmployeeReq(string FullName, string Username, string? NewPassword, long Permissions, bool IsActive);
}




