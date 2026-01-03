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

            // إذا فيه منتجات لا تعيد الزرع
            if (await db.Products.AnyAsync())
                return Results.Ok(new { ok = true, already = true, count = await db.Products.CountAsync() });

            var now = DateTime.UtcNow;

            db.Products.AddRange(
                new Product { Name="ماء صغير", Barcode="1001", Category="مشروبات", Quantity=100, PurchasePrice=0.5m, SalePrice=1m, IsVatIncluded=true, MinQuantity=5, SoldQuantity=0, SupplierName="مورد افتراضي" },
                new Product { Name="عصير", Barcode="1002", Category="مشروبات", Quantity=80, PurchasePrice=1.5m, SalePrice=3m, IsVatIncluded=true, MinQuantity=5, SoldQuantity=0, SupplierName="مورد افتراضي" },
                new Product { Name="شيبس", Barcode="1003", Category="سناكات", Quantity=60, PurchasePrice=1m, SalePrice=2m, IsVatIncluded=true, MinQuantity=5, SoldQuantity=0, SupplierName="مورد افتراضي" }
            );

            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true, inserted = await db.Products.CountAsync() });
        });
    }
}
