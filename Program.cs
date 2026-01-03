using Npgsql;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;
using NaderProductsApp.Models;

var builder = WebApplication.CreateBuilder(args);

// --- PORT binding (Railway/Render) ---
var __envPort = Environment.GetEnvironmentVariable("PORT");
var __listenPort = 0;
if (!int.TryParse(__envPort, out __listenPort) || __listenPort <= 0) __listenPort = 8080;
var __urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
Console.WriteLine($"[BOOT] PORT='{__envPort}' ASPNETCORE_URLS='{__urlsEnv}' -> ListenAnyIP({__listenPort})");
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(__listenPort));
// ------------------------------------
builder.Services.AddDbContext<AppDbContext>(o =>
{
    // Render يرسل DATABASE_URL مثل: postgresql://user:pass@host:5432/db
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");

    if (!string.IsNullOrWhiteSpace(url))
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            Database = uri.AbsolutePath.Trim('/')};

        o.UseNpgsql(csb.ConnectionString);
    }
    else
    {
        // محلي
        o.UseSqlite("Data Source=naderpos.db");
    }
});
var app = builder.Build();

// DEBUG: رجّع تفاصيل أي خطأ 500 كنص (عشان ما تكون الأخطاء صامتة على Railway)
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.Response.WriteAsync(ex.ToString());
    }
});
AuthApiV1.EnsureDefaultAdmin(app.Services);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Try migrations first; if none exist, create schema
    try { db.Database.Migrate(); }
    catch { db.Database.EnsureCreated(); }

    // Safety: if schema still missing (no migrations), create tables
    try { db.Database.ExecuteSqlRaw("SELECT 1 FROM Products LIMIT 1;"); }
    catch { db.Database.EnsureCreated(); }
}

app.UseDefaultFiles();
app.UseStaticFiles();



app.MapGet("/health", () => Results.Ok("OK"));

static string NormPm(string? pm)
{
    pm = (pm ?? "").Trim().ToLowerInvariant();
    if (pm is "cash" or "card" or "deferred") return pm;
    return "cash";
}

// ======================= PRODUCTS =======================
app.MapGet("/api/products", async ([FromServices] AppDbContext db) =>
    Results.Ok(await db.Products.OrderByDescending(x => x.Id).ToListAsync()));

app.MapGet("/api/products/cashier", async ([FromServices] AppDbContext db) =>
{
    var list = await db.Products
        .Select(p => new {
            id = p.Id,
            name = p.Name,
            barcode = p.Barcode,
            salePrice = p.SalePrice,
            isVatIncluded = p.IsVatIncluded,
            offerEnabled = p.OfferEnabled,
            offerName = p.OfferName,
            offerPrice = p.OfferPrice,
            offerVatIncluded = p.OfferVatIncluded,
            offerStart = p.OfferStart,
            offerEnd = p.OfferEnd
        })
        .OrderByDescending(x => x.id)
        .ToListAsync();

    return Results.Ok(list);
});

app.MapPost("/api/products", async ([FromServices] AppDbContext db, Product input) =>
{
    input.Barcode = (input.Barcode ?? "").Trim();
    input.Name    = (input.Name ?? "").Trim();

    if (string.IsNullOrWhiteSpace(input.Barcode) || string.IsNullOrWhiteSpace(input.Name))
        return Results.BadRequest("REQUIRED_FIELDS");

    var exists = await db.Products.AnyAsync(p => p.Barcode == input.Barcode);
    if (exists) return Results.BadRequest("BARCODE_DUPLICATE");

    db.Products.Add(input);
    await db.SaveChangesAsync();
    return Results.Ok(input);
});

app.MapPut("/api/products/{id:int}", async ([FromServices] AppDbContext db, int id, Product input) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();

    input.Barcode = (input.Barcode ?? "").Trim();
    input.Name    = (input.Name ?? "").Trim();

    if (string.IsNullOrWhiteSpace(input.Barcode) || string.IsNullOrWhiteSpace(input.Name))
        return Results.BadRequest("REQUIRED_FIELDS");

    var dup = await db.Products.AnyAsync(x => x.Id != id && x.Barcode == input.Barcode);
    if (dup) return Results.BadRequest("BARCODE_DUPLICATE");

    p.Barcode = input.Barcode;
    p.Name = input.Name;
    p.SupplierName = input.SupplierName;
    p.Category = input.Category;
    p.Quantity = input.Quantity;
    p.MinQuantity = input.MinQuantity;
    p.SoldQuantity = input.SoldQuantity;
    p.PurchasePrice = input.PurchasePrice;
    p.SalePrice = input.SalePrice;
    p.IsVatIncluded = input.IsVatIncluded;
    p.ExpiryDate = input.ExpiryDate;
    p.OfferEnabled = input.OfferEnabled;
    p.OfferName = input.OfferName;
    p.OfferPrice = input.OfferPrice;
    p.OfferVatIncluded = input.OfferVatIncluded;
    p.OfferStart = input.OfferStart;
    p.OfferEnd = input.OfferEnd;

    await db.SaveChangesAsync();
    return Results.Ok(p);
});

app.MapDelete("/api/products/{id:int}", async ([FromServices] AppDbContext db, int id) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();
    db.Products.Remove(p);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// ======================= CASHIER =======================
app.MapPost("/api/cashier/invoices", async ([FromServices] AppDbContext db, CashierInvoiceRequest req) =>
{
    if (req.Items is null || req.Items.Count == 0) return Results.BadRequest("EMPTY_ITEMS");

    var pm = NormPm(req.PaymentMethod);

    // تحقق المخزون أولاً
    foreach (var it in req.Items)
    {
        if (it.Quantity <= 0) return Results.BadRequest("INVALID_QTY");
        if (it.ProductId > 0)
        {
            var pr = await db.Products.FirstOrDefaultAsync(p => p.Id == it.ProductId);
            if (pr is null) return Results.BadRequest("PRODUCT_NOT_FOUND:" + it.ProductId);
            var qNeed = (int)Math.Ceiling(it.Quantity);
            if (pr.Quantity < qNeed) return Results.BadRequest("OUT_OF_STOCK:" + pr.Barcode);
        }
    }

    var inv = new CashierInvoice
    {
        InvoiceDate = DateTime.Now,
        PaymentMethod = pm,
        CustomerId = req.CustomerId,
        Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes,
        SubTotal = Math.Round(Convert.ToDecimal(req.SubTotal), 2),
        VatTotal = Math.Round(Convert.ToDecimal(req.VatTotal), 2),
        DiscountTotal = Math.Round(Convert.ToDecimal(req.DiscountTotal), 2),
        GrandTotal = Math.Round(Convert.ToDecimal(req.GrandTotal), 2),
        IsSuspended = false
    };

    foreach (var it in req.Items)
    {
        if (it.ProductId > 0)
        {
            var pr = await db.Products.FirstAsync(p => p.Id == it.ProductId);
            var q = (int)Math.Ceiling(it.Quantity);
            pr.Quantity -= q;
            pr.SoldQuantity += q;
        }

        inv.Items.Add(new CashierInvoiceItem
        {
            ProductId = it.ProductId > 0 ? it.ProductId : null,
            Barcode = it.Barcode,
            ProductName = (it.ProductName ?? "").Trim(),
            Quantity = Convert.ToDecimal(it.Quantity),
            UnitPrice = Convert.ToDecimal(it.UnitPrice),
            Discount = Convert.ToDecimal(it.Discount),
            TaxIncluded = it.TaxIncluded,
            HasOffer = it.HasOffer,
            OfferName = it.OfferName
        });
    }

    db.CashierInvoices.Add(inv);
    await db.SaveChangesAsync();

    // SYNC_DEFERRED_TO_CUSTOMER_LEDGER
    // إذا كانت الفاتورة "مؤجل" وعلى عميل -> سجلها كسند على العميل لعرضها في customers.html
    if (pm == "deferred" && inv.CustomerId.HasValue && inv.CustomerId.Value > 0)
    {
        var ledger = new CustomerInvoice
        {
            CustomerId = inv.CustomerId.Value,
            Amount = (double)inv.GrandTotal,
            Description = "فاتورة كاشير رقم " + inv.Id,
            Date = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
        };
        db.CustomerInvoices.Add(ledger);
        await db.SaveChangesAsync();
    }
    return Results.Ok(new { id = inv.Id });
});

app.MapGet("/api/cashier/invoices/{id:int}/items", async ([FromServices] AppDbContext db, int id) =>
{
    var inv = await db.CashierInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();

    var items = await db.CashierInvoiceItems.AsNoTracking()
        .Where(x => x.CashierInvoiceId == id)
        .OrderBy(x => x.Id)
        .Select(x => new {
            id = x.Id,
            productName = x.ProductName,
            barcode = x.Barcode,
            quantity = x.Quantity,
            price = x.UnitPrice,
            discount = x.Discount,
            taxIncluded = x.TaxIncluded
        })
        .ToListAsync();

    return Results.Ok(items);
});

app.MapGet("/api/cashier/invoices/report", async ([FromServices] AppDbContext db,
    [FromQuery] int? invoiceId,
    [FromQuery] string? from,
    [FromQuery] string? to,
    [FromQuery] string? paymentMethod,
    [FromQuery] string? status,
    [FromQuery] string? returnFilter) =>
{
    var q = db.CashierInvoices.AsNoTracking().AsQueryable();

    if (invoiceId.HasValue) q = q.Where(x => x.Id == invoiceId.Value);

    if (!string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var df))
        q = q.Where(x => x.InvoiceDate.Date >= df.Date);

    if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var dt))
        q = q.Where(x => x.InvoiceDate.Date <= dt.Date);

    var pm = (paymentMethod ?? "").Trim().ToLowerInvariant();
    if (pm is "cash" or "card" or "deferred") q = q.Where(x => x.PaymentMethod == pm);

    var st = (status ?? "").Trim().ToLowerInvariant();
    if (st == "normal") q = q.Where(x => !x.IsSuspended);
    if (st == "suspended") q = q.Where(x => x.IsSuspended);

    var rf = (returnFilter ?? "").Trim().ToLowerInvariant();
    if (rf == "with") q = q.Where(x => x.ReturnAmount > 0.0001m);
    if (rf == "without") q = q.Where(x => x.ReturnAmount <= 0.0001m);

    var list = await q.OrderByDescending(x => x.Id)
        .Select(x => new {
            id = x.Id,
            invoiceDate = x.InvoiceDate,
            customerName = x.CustomerName,
            customerPhone = x.CustomerPhone,
            paymentMethod = x.PaymentMethod,
            isSuspended = x.IsSuspended,
            discountTotal = x.DiscountTotal,
            vatTotal = x.VatTotal,
            grandTotal = x.GrandTotal,
            returnAmount = x.ReturnAmount
        })
        .ToListAsync();

    return Results.Ok(list);
});

app.MapPost("/api/cashier/invoices/{id:int}/return", async ([FromServices] AppDbContext db, int id, ReturnRequest req) =>
{
    if (req.Items is null || req.Items.Count == 0) return Results.BadRequest("EMPTY_RETURN");

    var inv = await db.CashierInvoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();

    decimal retSum = 0m;

    foreach (var r in req.Items)
    {
        var item = inv.Items.FirstOrDefault(x => x.Id == r.ItemId);
        if (item is null) return Results.BadRequest("ITEM_NOT_FOUND:" + r.ItemId);
        var rq = Convert.ToDecimal(r.ReturnQuantity);
if (rq <= 0m) return Results.BadRequest("INVALID_RETURN_QTY");
if (rq > item.Quantity) return Results.BadRequest("RETURN_EXCEEDS_ORIGINAL");// ✅ ثبت المرتجع على نفس سطر الفاتورة (عشان شاشة العملاء/عرض المواد تكون صحيحة)
        item.Quantity = Math.Max(0m, item.Quantity - rq);
if (item.ProductId.HasValue && item.ProductId.Value > 0)
        {
            var pr = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId.Value);
            if (pr != null)
            {
                var qret = (int)Math.Ceiling((double)rq);
                pr.Quantity += qret;
                pr.SoldQuantity = Math.Max(0, pr.SoldQuantity - qret);
            }
        }

        retSum += (item.UnitPrice * rq);
    }

    inv.ReturnAmount = Math.Round(inv.ReturnAmount + retSum, 2);

// ✅ SYNC_RETURN_TO_CUSTOMER_LEDGER (للمؤجل فقط)
if (inv.PaymentMethod == "deferred" && inv.CustomerId.HasValue && inv.CustomerId.Value > 0 && retSum > 0.0001m)
{
    db.CustomerInvoices.Add(new CustomerInvoice
    {
        CustomerId = inv.CustomerId.Value,
        Amount = -(double)Math.Round(retSum, 2),
        Description = "مرتجع فاتورة كاشير رقم " + inv.Id,
        Date = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
    });
}

await db.SaveChangesAsync();
return Results.Ok(new { ok = true, returnAdded = Math.Round(retSum, 2), invoiceReturnAmount = inv.ReturnAmount });
});

app.MapDelete("/api/cashier/invoices/{id:int}", async ([FromServices] AppDbContext db, int id) =>
{
    var inv = await db.CashierInvoices.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();

    foreach (var item in inv.Items)
    {
        if (item.ProductId.HasValue && item.ProductId.Value > 0)
        {
            var pr = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId.Value);
            if (pr != null)
            {
                var q = (int)Math.Ceiling(item.Quantity);
                pr.Quantity += q;
                pr.SoldQuantity = Math.Max(0, pr.SoldQuantity - q);
            }
        }
    }

    db.CashierInvoices.Remove(inv);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// ======================= CUSTOMERS (السبب حق 404) =======================
app.MapGet("/api/customers/full", async ([FromServices] AppDbContext db) =>
{
    var list = await db.Customers
        .Include(x => x.Invoices)
        .Include(x => x.Payments)
        .OrderByDescending(x => x.Id)
        .ToListAsync();

    return Results.Ok(list.Select(c => new {
        id = c.Id,
        name = c.Name,
        phone = c.Phone,
        address = c.Address,
        notes = c.Notes,
        status = c.Status,
        invoices = (c.Invoices ?? new List<CustomerInvoice>()).OrderByDescending(x => x.Id).Select(x => new {
            id = x.Id, amount = x.Amount, description = x.Description, date = x.Date
        }),
        payments = (c.Payments ?? new List<CustomerPayment>()).OrderByDescending(x => x.Id).Select(x => new {
            id = x.Id, amount = x.Amount, method = x.Method, note = x.Note, date = x.Date
        })
    }));
});

app.MapPost("/api/customers", async ([FromServices] AppDbContext db, Customer input) =>
{
    input.Name = (input.Name ?? "").Trim();
    if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest("REQUIRED_NAME");

    input.Phone = (input.Phone ?? "").Trim();
    input.Address = (input.Address ?? "").Trim();
    input.Notes = (input.Notes ?? "").Trim();
    input.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status;

    db.Customers.Add(input);
    await db.SaveChangesAsync();
    return Results.Ok(new { id = input.Id });
});
// ===== CUSTOMERS CRUD (customers.html) =====
app.MapPut("/api/customers/{id:int}", async ([FromServices] AppDbContext db, int id, Customer input) =>
{
    var c = await db.Customers.FindAsync(id);
    if (c is null) return Results.NotFound();

    c.Name = (input.Name ?? "").Trim();
    if (string.IsNullOrWhiteSpace(c.Name)) return Results.BadRequest("REQUIRED_NAME");

    c.Phone = (input.Phone ?? "").Trim();
    c.Address = (input.Address ?? "").Trim();
    c.Notes = (input.Notes ?? "").Trim();
    c.Status = string.IsNullOrWhiteSpace(input.Status) ? c.Status : input.Status.Trim();

    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

app.MapDelete("/api/customers/{id:int}", async ([FromServices] AppDbContext db, int id) =>
{
    var c = await db.Customers
        .Include(x => x.Invoices)
        .Include(x => x.Payments)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (c is null) return Results.NotFound();

    db.CustomerInvoices.RemoveRange(c.Invoices);
    db.CustomerPayments.RemoveRange(c.Payments);
    db.Customers.Remove(c);
    await db.SaveChangesAsync();

    return Results.Ok(new { ok = true });
});

// ===== CUSTOMER PAYMENTS EDIT/DELETE =====
app.MapPut("/api/customer-payments/{id:int}", async ([FromServices] AppDbContext db, int id, CustomerPaymentRequest req) =>
{
    var p = await db.CustomerPayments.FindAsync(id);
    if (p is null) return Results.NotFound();

    if (req.Amount <= 0) return Results.BadRequest("INVALID_AMOUNT");

    p.Amount = req.Amount;
    p.Method = string.IsNullOrWhiteSpace(req.Method) ? p.Method : req.Method.Trim();
    p.Note   = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

    // customers.html قد يرسل paymentDate بدل date
    var d = string.IsNullOrWhiteSpace(req.Date) ? (string.IsNullOrWhiteSpace(req.PaymentDate) ? null : req.PaymentDate) : req.Date;
    if (!string.IsNullOrWhiteSpace(d)) p.Date = d!;

    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

app.MapDelete("/api/customer-payments/{id:int}", async ([FromServices] AppDbContext db, int id) =>
{
    var p = await db.CustomerPayments.FindAsync(id);
    if (p is null) return Results.NotFound();

    db.CustomerPayments.Remove(p);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// BEGIN_CUSTOMER_LEDGER_API
app.MapPost("/api/customers/{id:int}/invoices", async ([FromServices] AppDbContext db, int id, CustomerLedgerRequest req) =>
{
    var c = await db.Customers.FindAsync(id);
    if (c is null) return Results.NotFound();

    if (req.Amount <= 0) return Results.BadRequest("INVALID_AMOUNT");

    var inv = new CustomerInvoice
    {
        CustomerId = id,
        Amount = req.Amount,
        Description = string.IsNullOrWhiteSpace(req.Description) ? "تعديل رصيد يدوي" : req.Description.Trim(),
        Date = string.IsNullOrWhiteSpace(req.Date) ? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") : req.Date!
    };

    db.CustomerInvoices.Add(inv);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/customers/{id:int}/payments", async ([FromServices] AppDbContext db, int id, CustomerPaymentRequest req) =>
{
    var c = await db.Customers.FindAsync(id);
    if (c is null) return Results.NotFound();

    if (req.Amount <= 0) return Results.BadRequest("INVALID_AMOUNT");

    var pay = new CustomerPayment
    {
        CustomerId = id,
        Amount = req.Amount,
        Method = string.IsNullOrWhiteSpace(req.Method) ? "كاش" : req.Method.Trim(),
        Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        Date = string.IsNullOrWhiteSpace(req.Date) ? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") : req.Date!
    };

    db.CustomerPayments.Add(pay);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});
// END_CUSTOMER_LEDGER_API

app.Urls.Clear();
var __bindPort = Environment.GetEnvironmentVariable("PORT") ?? "5050";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{__bindPort}");
AuthApiV1.MapAuthApi(app);
SettingsApiV1.Map(app);
CashiersApiV1.Map(app);
ExpensesApiV1.Map(app);
EmployeesApiV1.MapEmployeesApi(app);
ShiftsApiV1.MapShiftsApi(app);
ShiftsApiV1.MapShiftListApi(app);
app.Run();






























