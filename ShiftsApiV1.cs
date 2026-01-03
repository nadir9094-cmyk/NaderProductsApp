using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class ShiftsApiV1
{
    // ملاحظة: هذا API "آمن" حتى لو جدول الشفتات لسه ما عندك عليه موديل كامل
    // الفكرة: نمنع 404، ونرجّع استجابة مفهومة للكاشير، وبعدها نكمّل ربط الشفتات بالكامل.
    public static void MapShiftsApi(WebApplication app)
    {
        // الكاشير ينادي هذا
        app.MapGet("/api/shifts/current", async ([FromServices] AppDbContext db, HttpRequest http) =>
        {
            // إذا نظامك يشترط توكن: خليها Unauthorized بدل 404
            // (الكاشير عندك يرسل Bearer من emp_token)
            // لو ما فيه توكن لا نطيح الصفحة
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();

            // مؤقتًا: نرجّع "لا يوجد شفت مفتوح"
            // عشان الكاشير ما ينهار، وبعدها نربط فتح/إغلاق الشفت بشكل كامل
            return Results.Ok(new { open = false, id = 0 });
        });

        // فتح شفت (مؤقت)
        app.MapPost("/api/shifts/open", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();
            return Results.Ok(new { ok = true, id = 1, open = true });
        });

        // إغلاق شفت (مؤقت)
        app.MapPost("/api/shifts/close", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();
            return Results.Ok(new { ok = true, closed = true });
        });
    }
}
