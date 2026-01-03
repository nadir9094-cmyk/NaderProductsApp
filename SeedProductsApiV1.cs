using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class SeedProductsApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/_diag/seed/products", async (AppDbContext db, HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            // ملاحظة: endpoint هذا للتجارب فقط
            // نزرع في جدول Products مباشرة (SQL) عشان ما نعتمد على Entity اسمها Product

            // 1) تأكد أن الجدول موجود
            // 2) إذا فيه بيانات لا نعيد الزرع
            var count = await db.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Products') THEN
    RAISE EXCEPTION 'Products table not found';
  END IF;
END $$;
");

            // هل فيه منتجات؟
            var exists = await db.Set<TempCount>().FromSqlRaw(@"SELECT COUNT(*)::int AS ""Value"" FROM ""Products""").ToListAsync();
            var c = exists.Count > 0 ? exists[0].Value : 0;
            if (c > 0)
                return Results.Ok(new { ok = true, already = true, count = c });

            // زرع 3 منتجات (أعمدة شائعة عندك)
            // إذا بعض الأعمدة غير موجودة في جدولك، قلّي ونعمل نسخة توافق جدولك 1:1
            await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO ""Products"" (""Name"", ""Barcode"", ""Category"", ""Quantity"", ""PurchasePrice"", ""SalePrice"", ""IsVatIncluded"", ""MinQuantity"", ""SoldQuantity"", ""SupplierName"")
VALUES
('ماء صغير','1001','مشروبات',100,0.5,1.0,true,5,0,'مورد افتراضي'),
('عصير','1002','مشروبات',80,1.5,3.0,true,5,0,'مورد افتراضي'),
('شيبس','1003','سناكات',60,1.0,2.0,true,5,0,'مورد افتراضي');
");

            var after = await db.Set<TempCount>().FromSqlRaw(@"SELECT COUNT(*)::int AS ""Value"" FROM ""Products""").ToListAsync();
            return Results.Ok(new { ok = true, inserted = after[0].Value });
        });
    }

    private class TempCount
    {
        public int Value { get; set; }
    }
}
