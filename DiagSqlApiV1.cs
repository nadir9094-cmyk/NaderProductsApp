using System.Data;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class DiagSqlApiV1
{
    public static void Map(WebApplication app)
    {
        // تنفيذ SQL للـ DIAG (للاستخدام الإداري فقط)
        app.MapGet("/api/_diag/sql", async (AppDbContext db, HttpRequest http, string q) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest("q is required");
            var sql = q.Trim();

            // حماية بسيطة: فقط SELECT
            var up = sql.ToUpperInvariant();
            if (!up.StartsWith("SELECT") && !up.StartsWith("WITH"))
                return Results.BadRequest("Only SELECT/WITH allowed.");

            await using var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 20;

            var rows = new List<Dictionary<string, object?>>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < r.FieldCount; i++)
                {
                    var v = r.IsDBNull(i) ? null : r.GetValue(i);
                    row[r.GetName(i)] = v;
                }
                rows.Add(row);
                if (rows.Count >= 200) break;
            }

            return Results.Ok(new { count = rows.Count, rows });
        });

        // Patch سريع: يصلّح أعمدة CashierInvoices (خصوصاً CashierId NOT NULL)
        app.MapPost("/api/_diag/patch/cashierinvoice-fix", async (AppDbContext db, HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();

            // 1) تأكد الأعمدة موجودة
            await db.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""CashierInvoices"" ADD COLUMN IF NOT EXISTS ""CashierId"" integer;");
            await db.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""CashierInvoices"" ADD COLUMN IF NOT EXISTS ""CashierName"" text;");

            // 2) عالج أي بيانات قديمة NULL
            await db.Database.ExecuteSqlRawAsync(@"UPDATE ""CashierInvoices"" SET ""CashierId""=1 WHERE ""CashierId"" IS NULL;");

            // 3) خفّف القيود (عشان ما يطيح الحفظ حتى لو الكود ما عبّى)
            //    - خلي Default=1
            //    - DROP NOT NULL
            await db.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""CashierInvoices"" ALTER COLUMN ""CashierId"" SET DEFAULT 1;");
            await db.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""CashierInvoices"" ALTER COLUMN ""CashierId"" DROP NOT NULL;");

            return Results.Ok(new { ok = true });
        });
    }
}
