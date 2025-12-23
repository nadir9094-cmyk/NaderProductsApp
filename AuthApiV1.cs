using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Models;

using AppDbContext = NaderProductsApp.Data.AppDbContext;
using NaderProductsApp.Data;
public static class AuthApiV1
{
    private static string ToB64(byte[] b) => Convert.ToBase64String(b);
    private static byte[] FromB64(string s) => Convert.FromBase64String(s);

    private static (string hashB64, string saltB64) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 120_000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        return (ToB64(hash), ToB64(salt));
    }

    private static bool Verify(string password, string hashB64, string saltB64)
    {
        var salt = FromB64(saltB64);
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 120_000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(hash, FromB64(hashB64));
    }

    public static void EnsureDefaultAdmin(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        // إذا ما فيه ولا موظف: أنشئ مدير افتراضي
        if (!db.Employees.Any())
        {
            var (h, s) = HashPassword("admin123");
            db.Employees.Add(new Employee
            {
                FullName = "مدير النظام",
                Username = "admin",
                PasswordHash = h,
                PasswordSalt = s,
                Permissions = (long)EmployeePermissions.All,
                IsActive = true
            });
            db.SaveChanges();
        }
    }

    public static void MapAuthApi(WebApplication app)
    {
        app.MapPost("/api/auth/login", async (AppDbContext db, LoginReq req) =>
        {
            var u = (req.Username ?? "").Trim().ToLowerInvariant();
            var p = req.Password ?? "";
            if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p))
                return Results.BadRequest("INVALID_LOGIN");

            var emp = await db.Employees.FirstOrDefaultAsync(x => x.Username.ToLower() == u);
            if (emp is null || !emp.IsActive) return Results.Unauthorized();
            if (!Verify(p, emp.PasswordHash, emp.PasswordSalt)) return Results.Unauthorized();

            // create session
            var token = Guid.NewGuid().ToString("N");
            var session = new EmployeeSession
            {
                EmployeeId = emp.Id,
                Token = token,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            };
            db.EmployeeSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                token,
                employee = new { id = emp.Id, fullName = emp.FullName, username = emp.Username, permissions = emp.Permissions }
            });
        });

        app.MapPost("/api/auth/logout", async (AppDbContext db, HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token)) return Results.Ok();
            var s = await db.EmployeeSessions.FirstOrDefaultAsync(x => x.Token == token);
            if (s != null)
            {
                db.EmployeeSessions.Remove(s);
                await db.SaveChangesAsync();
            }
            return Results.Ok();
        });

        app.MapGet("/api/auth/me", async (AppDbContext db, HttpRequest http) =>
        {
            var emp = await EmployeesApiV1.GetEmployeeFromToken(db, http);
            if (emp is null) return Results.Unauthorized();
            return Results.Ok(new { id = emp.Id, fullName = emp.FullName, username = emp.Username, permissions = emp.Permissions });
        });
    }

    public record LoginReq(string Username, string Password);
}




