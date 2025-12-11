using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// اتصال PostgreSQL (Render)
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=dpg-d4p8e96uk2gs73d7hqv0-a.virginia-postgres.render.com;Port=5432;Database=naderposdb;Username=naderuser;Password=ESleHPj9Ux6m52uDtFkLoWa1lA4XaTMo;Ssl Mode=Require;Trust Server Certificate=true;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// إعادة توجيه للرئيسية
app.MapGet("/", () => Results.Redirect("/index.html"));

// ================== API المنتجات ==================
app.MapGet("/api/products", async (AppDbContext db) =>
    await db.Products.OrderBy(p => p.Id).ToListAsync());

// ================== حفظ فاتورة الكاشير ==================
app.MapPost("/api/cashier/invoices", async (CashierInvoiceRequest req, AppDbContext db) =>
{
    Console.WriteLine("=== RAW CASHIER REQUEST JSON ===");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(req));
    Console.WriteLine($"CASHIER REQ => SubTotal={req.SubTotal}, DiscountTotal={req.DiscountTotal}, VatTotal={req.VatTotal}, GrandTotal={req.GrandTotal}");

    var invoice = new CashierInvoice
    {
        InvoiceDate   = DateTime.SpecifyKind(req.InvoiceDate, DateTimeKind.Utc),
        PaymentMethod = req.PaymentMethod,
        CustomerName  = req.CustomerName,
        CustomerPhone = req.CustomerPhone,
        SubTotal      = req.SubTotal,
        DiscountTotal = req.DiscountTotal,
        VatTotal      = req.VatTotal,
        GrandTotal    = req.GrandTotal,
        IsSuspended   = req.IsSuspended,
        Notes         = req.Notes
    };

    db.CashierInvoices.Add(invoice);
    await db.SaveChangesAsync();

    if (req.Items != null)
    {
        foreach (var item in req.Items)
        {
            Product? product = null;

            if (!string.IsNullOrEmpty(item.Barcode))
            {
                product = await db.Products.FirstOrDefaultAsync(p => p.Barcode == item.Barcode);
                if (product != null)
                {
                    var qInt = (int)Math.Round(item.Quantity);
                    product.Quantity     = product.Quantity - qInt;
                    product.SoldQuantity = product.SoldQuantity + qInt;
                }
            }

            var name = string.IsNullOrWhiteSpace(item.ProductName)
                ? (product?.Name ?? "")
                : item.ProductName;

            var invItem = new CashierInvoiceItem
            {
                InvoiceId   = invoice.Id,
                Barcode     = item.Barcode,
                ProductName = name,
                Quantity    = item.Quantity,
                Price       = item.UnitPrice,
                Discount    = item.Discount,
                TaxIncluded = item.TaxIncluded ?? false,
                HasOffer    = item.HasOffer ?? false,
                OfferName   = string.IsNullOrWhiteSpace(item.OfferName) ? null : item.OfferName
            };

            db.CashierInvoiceItems.Add(invItem);
        }

        await db.SaveChangesAsync();
    }

    return Results.Ok(new { invoiceId = invoice.Id });
});

// ============== مرتجع فاتورة الكاشير ==============
app.MapPost("/api/cashier/invoices/{id:int}/return", async (int id, ReturnRequest req, AppDbContext db) =>
{
    var invoice = await db.CashierInvoices.FindAsync(id);
    if (invoice == null)
        return Results.NotFound(new { message = "الفاتورة غير موجودة" });

    if (req.Items == null || req.Items.Count == 0)
        return Results.BadRequest(new { message = "لم يتم تحديد أصناف للمرتجع" });

    decimal totalReturnAmount = 0m;

    foreach (var rItem in req.Items)
    {
        var invItem = await db.CashierInvoiceItems.FindAsync(rItem.ItemId);
        if (invItem == null || invItem.InvoiceId != id)
            continue;

        var availableQty = invItem.Quantity;
        var qty          = rItem.ReturnQuantity;

        if (qty <= 0 || availableQty <= 0)
            continue;

        if (qty > availableQty)
            qty = availableQty;

        // قيمة السطر قبل الخصم
        decimal lineGross    = invItem.Price * qty;
        decimal lineDiscount = 0m;

        // توزيع الخصم على الوحدات
        if (availableQty > 0 && invItem.Discount != 0)
        {
            var perUnitDiscount = invItem.Discount / availableQty;
            lineDiscount        = perUnitDiscount * qty;
        }

        var lineReturn = lineGross - lineDiscount;
        totalReturnAmount += lineReturn;

        // تعديل الكمية في سطر الفاتورة
        invItem.Quantity -= qty;
        if (invItem.Quantity < 0) invItem.Quantity = 0;

        // تعديل مخزون المنتج والكمية المباعة
        if (!string.IsNullOrWhiteSpace(invItem.Barcode))
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Barcode == invItem.Barcode);
            if (product != null)
            {
                var qtyInt = (int)Math.Round(qty);
                product.Quantity += qtyInt;

                if (product.SoldQuantity > 0)
                {
                    var newSold = product.SoldQuantity - qtyInt;
                    product.SoldQuantity = newSold < 0 ? 0 : newSold;
                }
            }
        }
    }

    // إجمالي المرتجع على مستوى الفاتورة
    if (totalReturnAmount > 0)
        invoice.ReturnTotal += decimal.Round(totalReturnAmount, 2);

    // ملاحظات المرتجع
    if (!string.IsNullOrWhiteSpace(req.Note))
    {
        if (string.IsNullOrWhiteSpace(invoice.Notes))
            invoice.Notes = req.Note;
        else
            invoice.Notes = invoice.Notes + " | " + req.Note;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        invoiceId    = invoice.Id,
        returnAmount = invoice.ReturnTotal,
        message      = "تم تنفيذ المرتجع"
    });
});
// ============ نهاية مرتجع فاتورة الكاشير ============
// ============== تقرير فواتير الكاشير ==============
app.MapGet("/api/cashier/invoices/report", async (
    int? invoiceId,
    DateTime? from,
    DateTime? to,
    string? paymentMethod,
    string? status,
    string? returnFilter,
    AppDbContext db) =>
{
    var query = db.CashierInvoices.AsQueryable();

    if (invoiceId.HasValue)
        query = query.Where(i => i.Id == invoiceId.Value);

    if (from.HasValue)
    {
        var f = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
        query = query.Where(i => i.InvoiceDate >= f);
    }

    if (to.HasValue)
    {
        var t = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
        query = query.Where(i => i.InvoiceDate <= t);
    }

    if (!string.IsNullOrWhiteSpace(paymentMethod))
        query = query.Where(i => i.PaymentMethod == paymentMethod);

    if (!string.IsNullOrWhiteSpace(status))
    {
        if (status == "suspended")
            query = query.Where(i => i.IsSuspended);
        else if (status == "normal")
            query = query.Where(i => !i.IsSuspended);
    }

    if (!string.IsNullOrWhiteSpace(returnFilter))
    {
        if (returnFilter == "withReturn")
            query = query.Where(i => i.ReturnTotal > 0);
        else if (returnFilter == "withoutReturn")
            query = query.Where(i => i.ReturnTotal == 0);
    }

    var list = await query
        .OrderByDescending(i => i.Id)
        .Take(500)
        .Select(i => new
        {
            id            = i.Id,
            invoiceDate   = i.InvoiceDate,
            paymentMethod = i.PaymentMethod,
            customerName  = i.CustomerName,
            customerPhone = i.CustomerPhone,
            subTotal      = i.SubTotal,
            discountTotal = i.DiscountTotal,
            vatTotal      = i.VatTotal,
            grandTotal    = i.GrandTotal,
            isSuspended   = i.IsSuspended,
            returnAmount  = i.ReturnTotal
        })
        .ToListAsync();

    return Results.Ok(list);
});

// ============== أصناف فاتورة الكاشير ==============
app.MapGet("/api/cashier/invoices/{id:int}/items", async (int id, AppDbContext db) =>
{
    var items = await db.CashierInvoiceItems
        .Where(i => i.InvoiceId == id)
        .OrderBy(i => i.Id)
        .Select(i => new {
            id          = i.Id,
            productName = i.ProductName,
            barcode     = i.Barcode,
            quantity    = i.Quantity,
            price       = i.Price,
            discount    = i.Discount,
            taxIncluded = i.TaxIncluded,
            hasOffer    = i.HasOffer,
            offerName   = i.OfferName
        })
        .ToListAsync();

    return Results.Ok(items);
});

// ============== حذف فاتورة الكاشير ==============
app.MapDelete("/api/cashier/invoices/{id:int}", async (int id, AppDbContext db) =>
{
    var invoice = await db.CashierInvoices.FindAsync(id);
    if (invoice == null)
        return Results.NotFound(new { message = "الفاتورة غير موجودة" });

    var items = await db.CashierInvoiceItems
        .Where(i => i.InvoiceId == id)
        .ToListAsync();

    if (items.Count > 0)
        db.CashierInvoiceItems.RemoveRange(items);

    db.CashierInvoices.Remove(invoice);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "تم حذف الفاتورة بنجاح" });
});

app.Run();// ================== EF Core Models ==================
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<CashierInvoice> CashierInvoices => Set<CashierInvoice>();
    public DbSet<CashierInvoiceItem> CashierInvoiceItems => Set<CashierInvoiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(p => p.Id).HasColumnName("Id");
            e.Property(p => p.Barcode).HasColumnName("Barcode");
            e.Property(p => p.Name).HasColumnName("Name");
            e.Property(p => p.Category).HasColumnName("Category");
            e.Property(p => p.ExpiryDate).HasColumnName("ExpiryDate");
            e.Property(p => p.IsVatIncluded).HasColumnName("IsVatIncluded");
            e.Property(p => p.MinQuantity).HasColumnName("MinQuantity");
            e.Property(p => p.OfferEnabled).HasColumnName("OfferEnabled");
            e.Property(p => p.OfferStart).HasColumnName("OfferStart");
            e.Property(p => p.OfferEnd).HasColumnName("OfferEnd");
            e.Property(p => p.OfferName).HasColumnName("OfferName");
            e.Property(p => p.OfferPrice).HasColumnName("OfferPrice");
            e.Property(p => p.OfferVatIncluded).HasColumnName("OfferVatIncluded");
            e.Property(p => p.PurchasePrice).HasColumnName("PurchasePrice");
            e.Property(p => p.Quantity).HasColumnName("Quantity");
            e.Property(p => p.SalePrice).HasColumnName("SalePrice");
            e.Property(p => p.SoldQuantity).HasColumnName("SoldQuantity");
            e.Property(p => p.SupplierName).HasColumnName("SupplierName");
        });

        modelBuilder.Entity<CashierInvoice>(e =>
        {
            e.ToTable("cashierinvoices");
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.InvoiceDate).HasColumnName("invoicedate");
            e.Property(c => c.PaymentMethod).HasColumnName("paymentmethod");
            e.Property(c => c.CustomerName).HasColumnName("customername");
            e.Property(c => c.CustomerPhone).HasColumnName("customerphone");
            e.Property(c => c.SubTotal).HasColumnName("subtotal");
            e.Property(c => c.DiscountTotal).HasColumnName("discounttotal");
            e.Property(c => c.VatTotal).HasColumnName("vattotal");
            e.Property(c => c.GrandTotal).HasColumnName("grandtotal");
                        e.Property(c => c.ReturnTotal).HasColumnName("returntotal");
            e.Property(c => c.IsSuspended).HasColumnName("issuspended");
            e.Property(c => c.Notes).HasColumnName("notes");
        });

        modelBuilder.Entity<CashierInvoiceItem>(e =>
        {
            e.ToTable("cashierinvoiceitems");
            e.Property(i => i.Id).HasColumnName("id");
            e.Property(i => i.InvoiceId).HasColumnName("invoiceid");
            e.Property(i => i.ProductName).HasColumnName("productname");
            e.Property(i => i.Barcode).HasColumnName("barcode");
            e.Property(i => i.Quantity).HasColumnName("quantity");
            e.Property(i => i.Price).HasColumnName("price");
            e.Property(i => i.Discount).HasColumnName("discount");
            e.Property(i => i.TaxIncluded).HasColumnName("taxincluded");
            e.Property(i => i.HasOffer).HasColumnName("hasoffer");
            e.Property(i => i.OfferName).HasColumnName("offername");
        });

        modelBuilder.Entity<CashierInvoiceItem>()
            .HasOne<CashierInvoice>()
            .WithMany()
            .HasForeignKey(i => i.InvoiceId);
    }
}

public class Product
{
    public int Id { get; set; }
    public string? Barcode { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsVatIncluded { get; set; }
    public int MinQuantity { get; set; }
    public bool OfferEnabled { get; set; }
    public DateTime? OfferStart { get; set; }
    public DateTime? OfferEnd { get; set; }
    public string? OfferName { get; set; }
    public decimal? OfferPrice { get; set; }
    public bool OfferVatIncluded { get; set; }
    public decimal PurchasePrice { get; set; }
    public int Quantity { get; set; }
    public decimal SalePrice { get; set; }
    public int SoldQuantity { get; set; }
    public string? SupplierName { get; set; }
}

public class CashierInvoice
{
    public int Id { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal ReturnTotal { get; set; }
    public bool IsSuspended { get; set; }
    public string? Notes { get; set; }
}

public class CashierInvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string? ProductName { get; set; }
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Discount { get; set; }
    public bool TaxIncluded { get; set; }
    public bool HasOffer { get; set; }
    public string? OfferName { get; set; }
}

public record CashierInvoiceItemRequest(
    string? Barcode,
    string? ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    bool? TaxIncluded,
    bool? HasOffer,
    string? OfferName
);

public record CashierInvoiceRequest(
    DateTime InvoiceDate,
    string PaymentMethod,
    string? CustomerName,
    string? CustomerPhone,
    decimal SubTotal,
    decimal DiscountTotal,
    decimal VatTotal,
    decimal GrandTotal,
    bool IsSuspended,
    string? Notes,
    List<CashierInvoiceItemRequest> Items
);

public record ReturnRequestItem(int ItemId, decimal ReturnQuantity);
public record ReturnRequest(List<ReturnRequestItem> Items, string? Note);
















