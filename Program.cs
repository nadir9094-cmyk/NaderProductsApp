using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;
using NaderProductsApp.Models;

var builder = WebApplication.CreateBuilder(args);

// قراءة نوع قاعدة البيانات واتصالها من متغيرات البيئة
var dbProvider = builder.Configuration["DB_PROVIDER"];      // postgres أو sqlite
var dbConnection = builder.Configuration["DB_CONNECTION"];  // تستخدم مع postgres

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(dbProvider) &&
        dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(dbConnection))
    {
        // استخدام PostgreSQL (مثلاً على Render)
        options.UseNpgsql(dbConnection);
    }
    else
    {
        // الوضع الافتراضي: SQLite ملف products.db (محليًا)
        options.UseSqlite("Data Source=products.db");
    }
});

var app = builder.Build();

// ضمان إنشاء قاعدة البيانات/الجداول (يعمل مع SQLite و PostgreSQL)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// API للمنتجات
app.MapGet("/api/products", async (AppDbContext db) =>
    await db.Products.ToListAsync());

app.MapGet("/api/products/{id:int}", async (AppDbContext db, int id) =>
    await db.Products.FindAsync(id) is Product p ? Results.Ok(p) : Results.NotFound());

app.MapPost("/api/products", async (AppDbContext db, Product p) =>
{
    db.Products.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/api/products/{p.Id}", p);
});

app.MapPut("/api/products/{id:int}", async (AppDbContext db, int id, Product updated) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();

    p.Barcode          = updated.Barcode;
    p.Name             = updated.Name;
    p.SupplierName     = updated.SupplierName;
    p.Category         = updated.Category;
    p.Quantity         = updated.Quantity;
    p.MinQuantity      = updated.MinQuantity;
    p.SoldQuantity     = updated.SoldQuantity;
    p.PurchasePrice    = updated.PurchasePrice;
    p.SalePrice        = updated.SalePrice;
    p.IsVatIncluded    = updated.IsVatIncluded;
    p.ExpiryDate       = updated.ExpiryDate;
    p.OfferEnabled     = updated.OfferEnabled;
    p.OfferName        = updated.OfferName;
    p.OfferPrice       = updated.OfferPrice;
    p.OfferStart       = updated.OfferStart;
    p.OfferEnd         = updated.OfferEnd;
    p.OfferVatIncluded = updated.OfferVatIncluded;

    await db.SaveChangesAsync();
    return Results.Ok(p);
});

app.MapDelete("/api/products/{id:int}", async (AppDbContext db, int id) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();
    db.Products.Remove(p);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// الملفات الثابتة (الواجهة products.html وغيره)
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
