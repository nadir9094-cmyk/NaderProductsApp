using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;
using NaderProductsApp.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite("Data Source=products.db");
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/products", async (AppDbContext db) =>
{
    var products = await db.Products.OrderBy(p => p.Id).ToListAsync();
    return Results.Ok(products);
});

app.MapPost("/api/products", async (AppDbContext db, Product product) =>
{
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/api/products/{product.Id}", product);
});

app.MapPut("/api/products/{id:int}", async (int id, AppDbContext db, Product updated) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    product.Barcode = updated.Barcode;
    product.Name = updated.Name;
    product.SupplierName = updated.SupplierName;
    product.Category = updated.Category;
    product.Quantity = updated.Quantity;
    product.MinQuantity = updated.MinQuantity;
    product.SoldQuantity = updated.SoldQuantity;
    product.PurchasePrice = updated.PurchasePrice;
    product.SalePrice = updated.SalePrice;
    product.IsVatIncluded = updated.IsVatIncluded;
    product.ExpiryDate = updated.ExpiryDate;
    product.OfferEnabled = updated.OfferEnabled;
    product.OfferStart = updated.OfferStart;
    product.OfferEnd = updated.OfferEnd;
    product.OfferPrice = updated.OfferPrice;

    await db.SaveChangesAsync();
    return Results.Ok(product);
});

app.MapDelete("/api/products/{id:int}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    db.Products.Remove(product);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
