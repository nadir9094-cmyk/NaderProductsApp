using Microsoft.EntityFrameworkCore;

public static class ShiftsOpenApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/shifts/open", async (AppDbContext db, HttpRequest http) =>
        {
            var emp = EmployeesApiV1.GetEmployeeFromRequest(http);
            if (emp == null)
                return Results.Unauthorized();

            var openShift = await db.Shifts.FirstOrDefaultAsync(s => s.ClosedAt == null);
            if (openShift != null)
                return Results.BadRequest(new { error = "SHIFT_ALREADY_OPEN", shiftId = openShift.Id });

            var shift = new Shift
            {
                OpenedAt = DateTime.UtcNow,
                EmployeeId = emp.Id,
                EmployeeName = emp.FullName
            };

            db.Shifts.Add(shift);
            await db.SaveChangesAsync();

            return Results.Ok(new { ok = true, shiftId = shift.Id, openedAt = shift.OpenedAt });
        });
    }
}
