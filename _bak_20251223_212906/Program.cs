using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;
using NaderProductsApp.Models;
// SQLite removed
var builder = WebApplication.CreateBuilder(args);


builder.Services.Configure<BackupSettings>(builder.Configuration.GetSection("BackupSettings"));
builder.Services.AddHostedService<DailyBackupService>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
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
app.MapPost("/api/cashier/invoices", async (HttpRequest http, [FromServices] AppDbContext db, CashierInvoiceRequest req) =>
{
    if (req.Items is null || req.Items.Count == 0) return Results.BadRequest("EMPTY_ITEMS");

    var pm = NormPm(req.PaymentMethod);
        var allowOutOfStock = http.Query["allowOutOfStock"] == "1";
        var warnings = new List<string>();

// تحقق المخزون أولاً
    foreach (var it in req.Items)
    {
        if (it.Quantity <= 0) return Results.BadRequest("INVALID_QTY");
        if (it.ProductId > 0)
        {
            var pr = await db.Products.FirstOrDefaultAsync(p => p.Id == it.ProductId);
            if (pr is null) return Results.BadRequest("PRODUCT_NOT_FOUND:" + it.ProductId);
            var qNeed = (int)Math.Ceiling(it.Quantity);
            if (pr.Quantity < qNeed) { if (!allowOutOfStock) return Results.BadRequest("OUT_OF_STOCK:" + pr.Barcode); warnings.Add("OUT_OF_STOCK:" + pr.Barcode); }
}
    }

    var inv = new CashierInvoice
    {
        InvoiceDate = DateTime.UtcNow,
        PaymentMethod = pm,
        CustomerId = req.CustomerId,
        Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes,
        SubTotal = Math.Round(req.SubTotal, 2),
        VatTotal = Math.Round(req.VatTotal, 2),
        DiscountTotal = Math.Round(req.DiscountTotal, 2),
        GrandTotal = Math.Round(req.GrandTotal, 2),
        IsSuspended = false
    };

    foreach (var it in req.Items)
    {
        if (it.ProductId > 0)
        {
            var pr = await db.Products.FirstAsync(p => p.Id == it.ProductId);
            var q = (int)Math.Ceiling(it.Quantity);
            if (allowOutOfStock) { pr.Quantity = Math.Max(0, pr.Quantity - q); } else { pr.Quantity -= q; }
            pr.SoldQuantity += q;
        }

        inv.Items.Add(new CashierInvoiceItem
        {
            ProductId = it.ProductId > 0 ? it.ProductId : null,
            Barcode = it.Barcode,
            ProductName = (it.ProductName ?? "").Trim(),
            Quantity = it.Quantity,
            UnitPrice = it.UnitPrice,
            Discount = it.Discount,
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
            Amount = inv.GrandTotal,
            Description = "فاتورة كاشير رقم " + inv.Id,
            Date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
        };
        db.CustomerInvoices.Add(ledger);
        await db.SaveChangesAsync();
    }
    return Results.Ok(new { id = inv.Id, warnings = warnings });
});

app.MapGet("/api/cashier/invoices/{id:int}", async ([FromServices] AppDbContext db, int id) =>
{
    var inv = await db.CashierInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (inv is null) return Results.NotFound();

    return Results.Ok(new {
        id = inv.Id,
        invoiceDate = inv.InvoiceDate,
        customerName = inv.CustomerName,
        customerPhone = inv.CustomerPhone,
        paymentMethod = inv.PaymentMethod,
        isSuspended = inv.IsSuspended,
        discountTotal = inv.DiscountTotal,
        vatTotal = inv.VatTotal,
        grandTotal = inv.GrandTotal,
        returnAmount = inv.ReturnAmount
    });
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
        q = q.Where(x => x.InvoiceDate >= df.Date);

    if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var dt))
        q = q.Where(x => x.InvoiceDate <= dt.Date);

    var pm = (paymentMethod ?? "").Trim().ToLowerInvariant();
    if (pm is "cash" or "card" or "deferred") q = q.Where(x => x.PaymentMethod == pm);

    var st = (status ?? "").Trim().ToLowerInvariant();
    if (st == "normal") q = q.Where(x => !x.IsSuspended);
    if (st == "suspended") q = q.Where(x => x.IsSuspended);

    var rf = (returnFilter ?? "").Trim().ToLowerInvariant();
    if (rf == "with") q = q.Where(x => x.ReturnAmount > 0.0001);
    if (rf == "without") q = q.Where(x => x.ReturnAmount <= 0.0001);

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

    double retSum = 0;

    foreach (var r in req.Items)
    {
        var item = inv.Items.FirstOrDefault(x => x.Id == r.ItemId);
        if (item is null) return Results.BadRequest("ITEM_NOT_FOUND:" + r.ItemId);
        if (r.ReturnQuantity <= 0) return Results.BadRequest("INVALID_RETURN_QTY");
        if (r.ReturnQuantity > item.Quantity) return Results.BadRequest("RETURN_EXCEEDS_ORIGINAL");

        // ✅ ثبت المرتجع على نفس سطر الفاتورة (عشان شاشة العملاء/عرض المواد تكون صحيحة)
        item.Quantity = Math.Max(0, item.Quantity - r.ReturnQuantity);
if (item.ProductId.HasValue && item.ProductId.Value > 0)
        {
            var pr = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId.Value);
            if (pr != null)
            {
                var qret = (int)Math.Ceiling(r.ReturnQuantity);
                pr.Quantity += qret;
                pr.SoldQuantity = Math.Max(0, pr.SoldQuantity - qret);
            }
        }

        retSum += (item.UnitPrice * r.ReturnQuantity);
    }

    inv.ReturnAmount = Math.Round(inv.ReturnAmount + retSum, 2);

// ✅ SYNC_RETURN_TO_CUSTOMER_LEDGER (للمؤجل فقط)
if (inv.PaymentMethod == "deferred" && inv.CustomerId.HasValue && inv.CustomerId.Value > 0 && retSum > 0.0001)
{
    db.CustomerInvoices.Add(new CustomerInvoice
    {
        CustomerId = inv.CustomerId.Value,
        Amount = -Math.Round(retSum, 2),
        Description = "مرتجع فاتورة كاشير رقم " + inv.Id,
        Date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
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
        Date = string.IsNullOrWhiteSpace(req.Date) ? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss") : req.Date!
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
        Date = string.IsNullOrWhiteSpace(req.Date) ? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss") : req.Date!
    };

    db.CustomerPayments.Add(pay);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});
// END_CUSTOMER_LEDGER_API


//
// EXPENSES_API_V1
//
app.MapGet("/api/expenses", async (AppDbContext db,
    string? q,
    DateTime? fromDate,
    DateTime? toDate,
    decimal? minAmount,
    decimal? maxAmount
) =>
{
    var query = db.Expenses.AsQueryable();

    if (fromDate.HasValue) query = query.Where(x => x.CreatedAt >= fromDate.Value.Date);
    if (toDate.HasValue)   query = query.Where(x => x.CreatedAt <= toDate.Value.Date);

    if (minAmount.HasValue) query = query.Where(x => x.Amount >= minAmount.Value);
    if (maxAmount.HasValue) query = query.Where(x => x.Amount <= maxAmount.Value);

    if (!string.IsNullOrWhiteSpace(q))
    {
        q = q.Trim();
        query = query.Where(x => (x.Statement ?? "").Contains(q));
    }

    var rows = await query
        .OrderByDescending(x => x.Date)
        .ThenByDescending(x => x.Id)
        .ToListAsync();

    var total = rows.Sum(x => x.Amount);
    return Results.Ok(new { total, rows });
});

app.MapPost("/api/expenses", async (AppDbContext db, NaderProductsApp.Models.Expense body) =>
{
    if (string.IsNullOrWhiteSpace(body.Statement))
        return Results.BadRequest("البيان مطلوب");

    if (body.Amount <= 0)
        return Results.BadRequest("المبلغ يجب أن يكون أكبر من صفر");

    var e = new NaderProductsApp.Models.Expense
    {
        Date = body.CreatedAt.Date,
        Statement = body.Statement.Trim(),
        Amount = body.Amount,
        CreatedAt = DateTime.UtcNow
    };

    db.Expenses.Add(e);
    await db.SaveChangesAsync();
    return Results.Ok(e);
});

app.MapPut("/api/expenses/{id:int}", async (AppDbContext db, int id, NaderProductsApp.Models.Expense body) =>
{
    var e = await db.Expenses.FindAsync(id);
    if (e is null) return Results.NotFound();

    if (string.IsNullOrWhiteSpace(body.Statement))
        return Results.BadRequest("البيان مطلوب");

    if (body.Amount <= 0)
        return Results.BadRequest("المبلغ يجب أن يكون أكبر من صفر");

    e.Date = body.CreatedAt.Date;
    e.Statement = body.Statement.Trim();
    e.Amount = body.Amount;
    e.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(e);
});

app.MapDelete("/api/expenses/{id:int}", async (AppDbContext db, int id) =>
{
    var e = await db.Expenses.FindAsync(id);
    if (e is null) return Results.NotFound();

    db.Expenses.Remove(e);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});



//
// ===================== BACKUP_FEATURE_V2 =====================
// UI: /backup.html
// Endpoints:
//   GET  /api/backup/list
//   POST /api/backup/run
//   POST /api/backup/restore/{name}   (FULL DB restore)
//   GET  /api/backup/download/{name}
// =====================
app.MapGet("/api/backup/list", (IOptions<BackupSettings> opt, IHostEnvironment env) =>
{
    var s = opt.Value ?? new BackupSettings();
    var dir = Path.Combine(env.ContentRootPath, s.OutputDir ?? "wwwroot/backups");
    Directory.CreateDirectory(dir);

    var files = new DirectoryInfo(dir).GetFiles("*.sql")
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .Select(f => new {
            name = f.Name,
            size = f.Length,
            modifiedLocal = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
        })
        .ToList();

    return Results.Json(new {
        settings = new { enabled = s.Enabled, timeLocal = s.TimeLocal, retainDays = s.RetainDays, outputDir = s.OutputDir ?? "wwwroot/backups" },
        files
    });
});

app.MapGet("/api/backup/download/{name}", (string name, IOptions<BackupSettings> opt, IHostEnvironment env) =>
{
    var s = opt.Value ?? new BackupSettings();
    var dir = Path.Combine(env.ContentRootPath, s.OutputDir ?? "wwwroot/backups");
    var safe = Path.GetFileName(name);
    var path = Path.Combine(dir, safe);
    if (!System.IO.File.Exists(path)) return Results.NotFound("File not found");
    return Results.File(System.IO.File.ReadAllBytes(path), "application/sql", safe);
});

app.MapPost("/api/backup/run", async (IConfiguration cfg, IOptions<BackupSettings> opt, IHostEnvironment env) =>
{
    var s = opt.Value ?? new BackupSettings();
    if (!s.Enabled) return Results.Problem("Backup disabled in settings.");

    var cs = cfg.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(cs)) return Results.Problem("Missing connection string: Default");

    var b = new Npgsql.NpgsqlConnectionStringBuilder(cs);

    var dir = Path.Combine(env.ContentRootPath, s.OutputDir ?? "wwwroot/backups");
    Directory.CreateDirectory(dir);

    var file = "backup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".sql";
    var path = Path.Combine(dir, file);

    // Requires: pg_dump available in PATH (PostgreSQL client tools)
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "pg_dump",
        Arguments = $"-h {b.Host} -p {b.Port} -U {b.Username} -d {b.Database} -F p -f \"{path}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    if (!string.IsNullOrWhiteSpace(b.Password))
        psi.Environment["PGPASSWORD"] = b.Password;

    try
    {
        var p = System.Diagnostics.Process.Start(psi);
        if (p == null) return Results.Problem("Failed to start pg_dump");
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            var err = await p.StandardError.ReadToEndAsync();
            return Results.Problem("pg_dump failed: " + err);
        }
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return Results.Problem("pg_dump not found. Install PostgreSQL client tools or add pg_dump to PATH.");
    }

    return Results.Ok(new { file });
});

app.MapPost("/api/backup/restore/{name}", async (string name, IConfiguration cfg, IOptions<BackupSettings> opt, IHostEnvironment env) =>
{
    var s = opt.Value ?? new BackupSettings();
    var cs = cfg.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(cs)) return Results.Problem("Missing connection string: Default");

    var b = new Npgsql.NpgsqlConnectionStringBuilder(cs);

    var dir = Path.Combine(env.ContentRootPath, s.OutputDir ?? "wwwroot/backups");
    var safe = Path.GetFileName(name);
    var path = Path.Combine(dir, safe);
    if (!System.IO.File.Exists(path)) return Results.NotFound("File not found");

    // FULL DB restore from SQL file (drops/creates/overwrites depend on the SQL content)
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "psql",
        Arguments = $"-h {b.Host} -p {b.Port} -U {b.Username} -d {b.Database} -v ON_ERROR_STOP=1 -f \"{path}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    if (!string.IsNullOrWhiteSpace(b.Password))
        psi.Environment["PGPASSWORD"] = b.Password;

    try
    {
        var p = System.Diagnostics.Process.Start(psi);
        if (p == null) return Results.Problem("Failed to start psql");
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            var err = await p.StandardError.ReadToEndAsync();
            return Results.Problem("Restore failed: " + err);
        }
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return Results.Problem("psql not found. Install PostgreSQL client tools or add psql to PATH.");
    }

    return Results.Ok(new { ok = true, restored = safe });
});
#region POSTGRES_ONLY_SETTINGS_SUPPLIERS
const string DefaultSuppliersJson = @"{""suppliers"":[],""invoices"":[],""payments"":[]}";
static async Task<NaderProductsApp.Models.AppSetting> EnsureSettingsRow(NaderProductsApp.Data.AppDbContext db)
{
    var row = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == 1);
    if (row is null)
    {
        row = new NaderProductsApp.Models.AppSetting
        {
            Id = 1,
            InvoicePaper = "Cashier",
            CashierPaperWidthMm = 80,
            BarcodeType = "QR",
            ZatcaPhase = 1
        };
        db.AppSettings.Add(row);
        await db.SaveChangesAsync();
    }
    return row;
}

static async Task<NaderProductsApp.Models.SuppliersStoreRow> EnsureSuppliersStoreRow(NaderProductsApp.Data.AppDbContext db)
{
    var row = await db.SuppliersStore.FirstOrDefaultAsync(x => x.Id == 1);
    if (row is null)
    {
        row = new NaderProductsApp.Models.SuppliersStoreRow { Id = 1, Json = DefaultSuppliersJson };
        db.SuppliersStore.Add(row);
        await db.SaveChangesAsync();
    }
    if (string.IsNullOrWhiteSpace(row.Json)) row.Json = DefaultSuppliersJson;
    return row;
}

app.MapGet("/api/settings", async (NaderProductsApp.Data.AppDbContext db) =>
{
    var row = await EnsureSettingsRow(db);
    return Results.Ok(new
    {
        storeName = row.StoreName ?? "",
        commercialRegister = row.CommercialRegister ?? "",
        vatNumber = row.VatNumber ?? "",
        storeAddress = row.StoreAddress ?? "",
        storePhone = row.StorePhone ?? "",
        invoicePaper = string.IsNullOrWhiteSpace(row.InvoicePaper) ? "Cashier" : row.InvoicePaper,
        cashierPaperWidthMm = row.CashierPaperWidthMm ?? 80,
        storeLogoBase64 = row.StoreLogoBase64 ?? "",
        invoiceFooterNotes = row.InvoiceFooterNotes ?? "",
        barcodeType = string.IsNullOrWhiteSpace(row.BarcodeType) ? "QR" : row.BarcodeType,
        zatcaPhase = row.ZatcaPhase ?? 1,
        phase2InvoiceHash = row.Phase2InvoiceHash ?? "",
        phase2Signature = row.Phase2Signature ?? "",
        phase2PublicKey = row.Phase2PublicKey ?? "",
        phase2CertificateSignature = row.Phase2CertificateSignature ?? ""
    });
});

app.MapPut("/api/settings", async (NaderProductsApp.Data.AppDbContext db, HttpRequest request) =>
{
    var dto = await request.ReadFromJsonAsync<NaderProductsApp.Models.SettingsDto>();
    if (dto is null) return Results.BadRequest("INVALID_BODY");

    var row = await EnsureSettingsRow(db);

    var invoicePaper = (dto.InvoicePaper ?? "Cashier").Trim();
    if (invoicePaper != "A4" && invoicePaper != "Cashier") invoicePaper = "Cashier";

    var barcodeType = (dto.BarcodeType ?? "QR").Trim().ToUpperInvariant();
    if (barcodeType != "QR") barcodeType = "QR";
    var phase = dto.ZatcaPhase == 2 ? 2 : 1;
    var width = dto.CashierPaperWidthMm <= 0 ? 80 : dto.CashierPaperWidthMm;
    if (width < 58) width = 58;
    if (width > 120) width = 120;

    var logo = (dto.StoreLogoBase64 ?? "").Trim();
    if (logo.Length > 2_000_000) return Results.BadRequest("LOGO_TOO_LARGE");

    row.StoreName = (dto.StoreName ?? "").Trim();
    row.CommercialRegister = (dto.CommercialRegister ?? "").Trim();
    row.VatNumber = (dto.VatNumber ?? "").Trim();
    row.StoreAddress = (dto.StoreAddress ?? "").Trim();
    row.StorePhone = (dto.StorePhone ?? "").Trim();

    row.InvoicePaper = invoicePaper;
    row.CashierPaperWidthMm = width;
    row.StoreLogoBase64 = logo;
    row.InvoiceFooterNotes = (dto.InvoiceFooterNotes ?? "").Trim();

    row.BarcodeType = barcodeType;
    row.ZatcaPhase = phase;

    row.Phase2InvoiceHash = phase == 2 ? (dto.Phase2InvoiceHash ?? "").Trim() : "";
    row.Phase2Signature = phase == 2 ? (dto.Phase2Signature ?? "").Trim() : "";
    row.Phase2PublicKey = phase == 2 ? (dto.Phase2PublicKey ?? "").Trim() : "";
    row.Phase2CertificateSignature = phase == 2 ? (dto.Phase2CertificateSignature ?? "").Trim() : "";

    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/suppliers-store", async (NaderProductsApp.Data.AppDbContext db) =>
{
    var row = await EnsureSuppliersStoreRow(db);
    return Results.Text(row.Json ?? DefaultSuppliersJson, "application/json; charset=utf-8");
});

app.MapPut("/api/suppliers-store", async (NaderProductsApp.Data.AppDbContext db, HttpRequest request) =>
{
    using var sr = new StreamReader(request.Body);
    var raw = await sr.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(raw)) return Results.BadRequest("EMPTY_BODY");

    var row = await EnsureSuppliersStoreRow(db);
    row.Json = raw;
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

#endregion


app.Run("http://127.0.0.1:5050");

record SettingsDto(
    string? StoreName,
    string? CommercialRegister,
    string? VatNumber,
    string? StoreAddress,
    string? StorePhone,

    string? InvoicePaper,
    int CashierPaperWidthMm,
    string? StoreLogoBase64,
    string? InvoiceFooterNotes,

    string? BarcodeType,
    int ZatcaPhase,

    string? Phase2InvoiceHash,
    string? Phase2Signature,
    string? Phase2PublicKey,
    string? Phase2CertificateSignature
);
//















