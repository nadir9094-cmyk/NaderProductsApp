using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Models.ControlPanel;

using AppDbContext = NaderProductsApp.Data.AppDbContext;
using NaderProductsApp.Data;
public static class ControlPanelApiV1
{
    private const string CP_HEADER = "X-CP-Token";

    public static void MapControlPanelApiV1(WebApplication app)
    {
        var g = app.MapGroup("/api/cp");
        g.MapPost("/bootstrap", Bootstrap); // create initial superadmin + plans
        g.MapPost("/login", Login);
        g.MapPost("/logout", Logout);
        g.MapGet("/me", Me);

        // tenants
        g.MapGet("/tenants", TenantsList);
        g.MapPost("/tenants", TenantsCreate);
        g.MapPut("/tenants/{id:int}", TenantsUpdate);
        g.MapDelete("/tenants/{id:int}", TenantsDelete);
    }

    private static async Task<IResult> Bootstrap([FromServices] AppDbContext db)
    {
        // Create default plans if not exist
        if(!await db.CpPlans.AnyAsync())
        {
            db.CpPlans.AddRange(
                new CpPlan{ Code="basic", Name="Basic", PriceMonthly=0m, MaxStores=1, MaxDbBytes=1L*1024*1024*1024 },
                new CpPlan{ Code="pro", Name="Pro", PriceMonthly=199m, MaxStores=1, MaxDbBytes=5L*1024*1024*1024 },
                new CpPlan{ Code="enterprise", Name="Enterprise", PriceMonthly=499m, MaxStores=5, MaxDbBytes=20L*1024*1024*1024 }
            );
        }

        // Create initial superadmin if not exist
        if(!await db.CpAdminUsers.AnyAsync())
        {
            var tempPass = "Admin@12345"; // change later from CP
            db.CpAdminUsers.Add(new CpAdminUser{
                Username="superadmin",
                FullName="Super Admin",
                PasswordHash = HashPassword(tempPass),
                IsActive=true
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { created=true, username="superadmin", tempPassword=tempPass, note="غيّر كلمة المرور بعد أول دخول" });
        }

        await db.SaveChangesAsync();
        return Results.Ok(new { created=false });
    }

    private static async Task<IResult> Login([FromServices] AppDbContext db, HttpContext ctx, [FromBody] LoginReq req)
    {
        var u = (req.Username ?? "").Trim().ToLowerInvariant();
        var p = req.Password ?? "";

        var admin = await db.CpAdminUsers.FirstOrDefaultAsync(x => x.Username.ToLower() == u && x.IsActive);
        if(admin is null) return Results.Unauthorized();

        if(!VerifyPassword(p, admin.PasswordHash)) return Results.Unauthorized();

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var s = new CpAdminSession{
            AdminUserId = admin.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Ip = ctx.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx.Request.Headers.UserAgent.ToString()
        };
        db.CpAdminSessions.Add(s);
        admin.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { token, admin = new { id=admin.Id, username=admin.Username, fullName=admin.FullName }});
    }

    private static async Task<IResult> Logout([FromServices] AppDbContext db, HttpContext ctx)
    {
        var token = GetToken(ctx);
        if(string.IsNullOrWhiteSpace(token)) return Results.Ok();

        var s = await db.CpAdminSessions.FirstOrDefaultAsync(x => x.Token == token && x.RevokedAt == null);
        if(s != null)
        {
            s.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return Results.Ok();
    }

    private static async Task<IResult> Me([FromServices] AppDbContext db, HttpContext ctx)
    {
        var admin = await RequireAdmin(db, ctx);
        if(admin is null) return Results.Unauthorized();
        return Results.Ok(new { id=admin.Id, username=admin.Username, fullName=admin.FullName, lastLoginAt=admin.LastLoginAt });
    }

    private static async Task<IResult> TenantsList([FromServices] AppDbContext db, HttpContext ctx)
    {
        var admin = await RequireAdmin(db, ctx);
        if(admin is null) return Results.Unauthorized();

        var list = await db.CpTenants.Include(t=>t.Plan).OrderByDescending(x=>x.Id).Select(x=> new {
            id=x.Id, tenantCode=x.TenantCode, ownerName=x.OwnerName, phone=x.Phone, email=x.Email,
            planId=x.PlanId, planName=x.Plan!.Name, planCode=x.Plan!.Code,
            startedAt=x.StartedAt, expiresAt=x.ExpiresAt, isActive=x.IsActive, currentDbBytes=x.CurrentDbBytes
        }).ToListAsync();

        var plans = await db.CpPlans.Where(p=>p.IsActive).OrderBy(p=>p.Id).Select(p=> new {
            id=p.Id, code=p.Code, name=p.Name, priceMonthly=p.PriceMonthly, maxDbBytes=p.MaxDbBytes
        }).ToListAsync();

        return Results.Ok(new { tenants=list, plans });
    }

    private static async Task<IResult> TenantsCreate([FromServices] AppDbContext db, HttpContext ctx, [FromBody] TenantReq req)
    {
        var admin = await RequireAdmin(db, ctx);
        if(admin is null) return Results.Unauthorized();

        var code = (req.TenantCode ?? "").Trim().ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(code)) return Results.BadRequest("TENANT_CODE_REQUIRED");

        if(await db.CpTenants.AnyAsync(x => x.TenantCode.ToLower() == code))
            return Results.BadRequest("TENANT_CODE_EXISTS");

        var plan = await db.CpPlans.FirstOrDefaultAsync(p => p.Id == req.PlanId && p.IsActive);
        if(plan is null) return Results.BadRequest("PLAN_NOT_FOUND");

        var t = new CpTenant{
            TenantCode = code,
            OwnerName = (req.OwnerName ?? "").Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone)? null : req.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email)? null : req.Email.Trim(),
            PlanId = plan.Id,
            StartedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMonths(1),
            IsActive = true
        };
        db.CpTenants.Add(t);
        await db.SaveChangesAsync();
        return Results.Ok(new { id=t.Id });
    }

    private static async Task<IResult> TenantsUpdate([FromServices] AppDbContext db, HttpContext ctx, int id, [FromBody] TenantReq req)
    {
        var admin = await RequireAdmin(db, ctx);
        if(admin is null) return Results.Unauthorized();

        var t = await db.CpTenants.FirstOrDefaultAsync(x => x.Id == id);
        if(t is null) return Results.NotFound();

        var code = (req.TenantCode ?? "").Trim().ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(code)) return Results.BadRequest("TENANT_CODE_REQUIRED");

        if(await db.CpTenants.AnyAsync(x => x.Id != id && x.TenantCode.ToLower() == code))
            return Results.BadRequest("TENANT_CODE_EXISTS");

        var plan = await db.CpPlans.FirstOrDefaultAsync(p => p.Id == req.PlanId && p.IsActive);
        if(plan is null) return Results.BadRequest("PLAN_NOT_FOUND");

        t.TenantCode = code;
        t.OwnerName = (req.OwnerName ?? "").Trim();
        t.Phone = string.IsNullOrWhiteSpace(req.Phone)? null : req.Phone.Trim();
        t.Email = string.IsNullOrWhiteSpace(req.Email)? null : req.Email.Trim();
        t.PlanId = plan.Id;

        if(req.ExpiresAtUtc.HasValue) t.ExpiresAt = DateTime.SpecifyKind(req.ExpiresAtUtc.Value, DateTimeKind.Utc);
        t.IsActive = req.IsActive;

        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> TenantsDelete([FromServices] AppDbContext db, HttpContext ctx, int id)
    {
        var admin = await RequireAdmin(db, ctx);
        if(admin is null) return Results.Unauthorized();

        var t = await db.CpTenants.FirstOrDefaultAsync(x => x.Id == id);
        if(t is null) return Results.NotFound();

        db.CpTenants.Remove(t);
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static string? GetToken(HttpContext ctx)
    {
        if(ctx.Request.Headers.TryGetValue(CP_HEADER, out var v)) return v.ToString();
        return null;
    }

    private static async Task<CpAdminUser?> RequireAdmin(AppDbContext db, HttpContext ctx)
    {
        var token = GetToken(ctx);
        if(string.IsNullOrWhiteSpace(token)) return null;

        var s = await db.CpAdminSessions.Include(x=>x.AdminUser)
            .FirstOrDefaultAsync(x => x.Token == token && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow);

        return s?.AdminUser;
    }

    // PBKDF2
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        return $"PBKDF2$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        try
        {
            var parts = stored.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length != 4) return false;
            if(parts[0] != "PBKDF2") return false;
            var iter = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var hash = Convert.FromBase64String(parts[3]);

            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iter, HashAlgorithmName.SHA256);
            var test = pbkdf2.GetBytes(hash.Length);
            return CryptographicOperations.FixedTimeEquals(test, hash);
        }
        catch { return false; }
    }

    public record LoginReq(string Username, string Password);
    public record TenantReq(string TenantCode, string OwnerName, string? Phone, string? Email, int PlanId, DateTime? ExpiresAtUtc, bool IsActive);
}




