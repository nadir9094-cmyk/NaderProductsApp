using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;
using NaderProductsApp.Models;

namespace NaderProductsApp
{
    // ================= DTOs + Helper =================

    public record CustomerDto(
        int Id,
        string Name,
        string? Phone,
        string? Address,
        string? Notes,
        string Status,
        decimal InvoicesTotal,
        decimal PaymentsTotal,
        decimal Remaining,
        DateTime? LastInvoiceDate
    );

    public record CustomerEditRequest(
        string Name,
        string? Phone,
        string? Address,
        string? Notes,
        string Status,
        decimal OpeningBalance
    );

    public record CustomerInvoiceRequest(
        decimal Amount,
        string Description,
        DateTime? InvoiceDate
    );

    public class CustomerPaymentInput
    {
        public decimal Amount { get; set; }
        public string? Method { get; set; }
        public string? Note { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    public static class CustomerCalc
    {
        public static (decimal invoicesTotal, decimal paymentsTotal, decimal remaining, DateTime? lastInvoiceDate)
            CalculateTotals(Customer c)
        {
            var invoicesTotal = c.Invoices?.Sum(i => i.Amount) ?? 0m;
            var paymentsTotal = c.Payments?.Sum(p => p.Amount) ?? 0m;
            var remaining = invoicesTotal - paymentsTotal;
            DateTime? lastInvoiceDate = c.Invoices?
                .OrderByDescending(i => i.InvoiceDate)
                .FirstOrDefault()?.InvoiceDate;

            return (invoicesTotal, paymentsTotal, remaining, lastInvoiceDate);
        }
    }

    // ================= Main App =================

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure database provider (PostgreSQL on Render, SQLite locally by default)
            var provider = Environment.GetEnvironmentVariable("DB_PROVIDER");
            var conn = Environment.GetEnvironmentVariable("DB_CONNECTION");
            var isPostgres = string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
                             && !string.IsNullOrWhiteSpace(conn);

            if (isPostgres)
            {
                builder.Services.AddDbContext<AppDbContext>(opt =>
                    opt.UseNpgsql(conn));
            }
            else
            {
                builder.Services.AddDbContext<AppDbContext>(opt =>
                    opt.UseSqlite("Data Source=products.db"));
            }

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            var app = builder.Build();

            // Ensure database and tables are created
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // يضمن وجود products.db محلياً + جدول Products
                db.Database.EnsureCreated();

                // على Render (PostgreSQL) ننشئ جداول العملاء إذا كانت غير موجودة
                if (isPostgres)
                {
                    var sql = @"
CREATE TABLE IF NOT EXISTS ""Customers"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Name"" VARCHAR(200) NOT NULL,
    ""Phone"" TEXT NULL,
    ""Address"" TEXT NULL,
    ""Notes"" TEXT NULL,
    ""Status"" VARCHAR(32) NOT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS ""CustomerInvoices"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""CustomerId"" INT NOT NULL REFERENCES ""Customers""(""Id"") ON DELETE CASCADE,
    ""InvoiceDate"" TIMESTAMPTZ NOT NULL,
    ""Description"" VARCHAR(500) NOT NULL,
    ""Amount"" NUMERIC(18,2) NOT NULL
);

CREATE TABLE IF NOT EXISTS ""CustomerPayments"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""CustomerId"" INT NOT NULL REFERENCES ""Customers""(""Id"") ON DELETE CASCADE,
    ""PaymentDate"" TIMESTAMPTZ NOT NULL,
    ""Amount"" NUMERIC(18,2) NOT NULL,
    ""Method"" VARCHAR(50) NOT NULL,
    ""Note"" TEXT NULL
);
";
                    db.Database.ExecuteSqlRaw(sql);
                }
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseCors();

            // ---------------- PRODUCTS API ----------------

            app.MapGet("/api/products", async (AppDbContext db) =>
                await db.Products
                    .AsNoTracking()
                    .OrderBy(p => p.Id)
                    .ToListAsync());

            app.MapGet("/api/products/{id:int}", async (AppDbContext db, int id) =>
            {
                var product = await db.Products.FindAsync(id);
                return product is null ? Results.NotFound() : Results.Ok(product);
            });

            app.MapPost("/api/products", async (AppDbContext db, Product input) =>
            {
                input.Id = 0;
                db.Products.Add(input);
                await db.SaveChangesAsync();
                return Results.Ok(new { success = true, id = input.Id });
            });

            app.MapPut("/api/products/{id:int}", async (AppDbContext db, int id, Product input) =>
            {
                var product = await db.Products.FindAsync(id);
                if (product is null) return Results.NotFound();

                product.Barcode = input.Barcode;
                product.Name = input.Name;
                product.SupplierName = input.SupplierName;
                product.Category = input.Category;
                product.Quantity = input.Quantity;
                product.MinQuantity = input.MinQuantity;
                product.SoldQuantity = input.SoldQuantity;
                product.PurchasePrice = input.PurchasePrice;
                product.SalePrice = input.SalePrice;
                product.IsVatIncluded = input.IsVatIncluded;
                product.ExpiryDate = input.ExpiryDate;

                product.OfferEnabled = input.OfferEnabled;
                product.OfferName = input.OfferName;
                product.OfferPrice = input.OfferPrice;
                product.OfferStart = input.OfferStart;
                product.OfferEnd = input.OfferEnd;
                product.OfferVatIncluded = input.OfferVatIncluded;

                await db.SaveChangesAsync();
                return Results.Ok(new { success = true, id = product.Id });
            });

            app.MapDelete("/api/products/{id:int}", async (AppDbContext db, int id) =>
            {
                var product = await db.Products.FindAsync(id);
                if (product is null) return Results.NotFound();

                db.Products.Remove(product);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });

            // ---------------- CUSTOMERS / DEFERRED PAYMENTS API ----------------

            // Get all customers with balances + nested invoices/payments (used by customers.html)
            app.MapGet("/api/customers/full", async (AppDbContext db) =>
            {
                var list = await db.Customers
                    .Include(c => c.Invoices)
                    .Include(c => c.Payments)
                    .OrderBy(c => c.Id)
                    .ToListAsync();

                var result = list.Select(c =>
                {
                    var totals = CustomerCalc.CalculateTotals(c);
                    return new
                    {
                        c.Id,
                        c.Name,
                        c.Phone,
                        c.Address,
                        c.Notes,
                        c.Status,
                        invoicesTotal = totals.invoicesTotal,
                        paymentsTotal = totals.paymentsTotal,
                        remaining = totals.remaining,
                        lastInvoiceDate = totals.lastInvoiceDate,
                        invoices = c.Invoices
                            .OrderBy(i => i.InvoiceDate)
                            .Select(i => new
                            {
                                i.Id,
                                customerId = i.CustomerId,
                                date = i.InvoiceDate,
                                description = i.Description,
                                amount = i.Amount
                            }),
                        payments = c.Payments
                            .OrderBy(p => p.PaymentDate)
                            .Select(p => new
                            {
                                p.Id,
                                customerId = p.CustomerId,
                                date = p.PaymentDate,
                                amount = p.Amount,
                                method = p.Method,
                                note = p.Note
                            })
                    };
                });

                return Results.Ok(result);
            });

            // Create customer (with optional opening balance)
            app.MapPost("/api/customers", async (AppDbContext db, CustomerEditRequest req) =>
            {
                if (string.IsNullOrWhiteSpace(req.Name))
                    return Results.BadRequest("اسم العميل مطلوب.");

                var customer = new Customer
                {
                    Name = req.Name.Trim(),
                    Phone = req.Phone?.Trim(),
                    Address = req.Address?.Trim(),
                    Notes = req.Notes?.Trim(),
                    Status = string.IsNullOrWhiteSpace(req.Status) ? "active" : req.Status,
                    CreatedAt = DateTime.UtcNow
                };

                db.Customers.Add(customer);
                await db.SaveChangesAsync();

                if (req.OpeningBalance > 0)
                {
                    var inv = new CustomerInvoice
                    {
                        CustomerId = customer.Id,
                        Amount = req.OpeningBalance,
                        Description = "رصيد افتتاحي",
                        InvoiceDate = DateTime.UtcNow
                    };
                    db.CustomerInvoices.Add(inv);
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { success = true, id = customer.Id });
            });

            // Update customer (can also add an extra opening balance if > 0)
            app.MapPut("/api/customers/{id:int}", async (AppDbContext db, int id, CustomerEditRequest req) =>
            {
                var customer = await db.Customers.FindAsync(id);
                if (customer is null) return Results.NotFound();

                customer.Name = req.Name.Trim();
                customer.Phone = req.Phone?.Trim();
                customer.Address = req.Address?.Trim();
                customer.Notes = req.Notes?.Trim();
                customer.Status = string.IsNullOrWhiteSpace(req.Status) ? "active" : req.Status;

                if (req.OpeningBalance > 0)
                {
                    var inv = new CustomerInvoice
                    {
                        CustomerId = customer.Id,
                        Amount = req.OpeningBalance,
                        Description = "رصيد افتتاحي مضاف من التعديل",
                        InvoiceDate = DateTime.UtcNow
                    };
                    db.CustomerInvoices.Add(inv);
                }

                await db.SaveChangesAsync();
                return Results.Ok(new { success = true, id = customer.Id });
            });

            // Change status (activate / suspend)
            app.MapPost("/api/customers/{id:int}/status", async (AppDbContext db, int id, string status) =>
            {
                var customer = await db.Customers.FindAsync(id);
                if (customer is null) return Results.NotFound();

                customer.Status = string.IsNullOrWhiteSpace(status) ? "active" : status;
                await db.SaveChangesAsync();
                return Results.Ok(new { success = true, id = customer.Id, status = customer.Status });
            });

            // Delete customer (only if remaining == 0)
            app.MapDelete("/api/customers/{id:int}", async (AppDbContext db, int id) =>
            {
                var customer = await db.Customers
                    .Include(c => c.Invoices)
                    .Include(c => c.Payments)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (customer is null) return Results.NotFound();

                var totals = CustomerCalc.CalculateTotals(customer);
                if (totals.remaining > 0)
                    return Results.BadRequest("لا يمكن حذف العميل طالما عليه مبالغ متبقية.");

                db.Customers.Remove(customer);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });

            // Add manual invoice (increase customer deferred amount)
            app.MapPost("/api/customers/{id:int}/invoices", async (AppDbContext db, int id, CustomerInvoiceRequest req) =>
            {
                var customer = await db.Customers.FindAsync(id);
                if (customer is null) return Results.NotFound();

                if (req.Amount <= 0) return Results.BadRequest("المبلغ يجب أن يكون أكبر من صفر.");

                var inv = new CustomerInvoice
                {
                    CustomerId = customer.Id,
                    Amount = req.Amount,
                    Description = string.IsNullOrWhiteSpace(req.Description) ? "تعديل رصيد يدوي" : req.Description.Trim(),
                    InvoiceDate = req.InvoiceDate ?? DateTime.UtcNow
                };

                db.CustomerInvoices.Add(inv);
                await db.SaveChangesAsync();
                return Results.Ok(new { success = true, id = inv.Id });
            });

            // Add payment
            app.MapPost("/api/customers/{id:int}/payments", async (AppDbContext db, int id, CustomerPaymentInput input) =>
            {
                try
                {
                    if (input == null)
                        return Results.BadRequest("بيانات الدفع غير صحيحة.");

                    if (input.Amount <= 0)
                        return Results.BadRequest("المبلغ يجب أن يكون أكبر من صفر.");

                    var customer = await db.Customers.FindAsync(id);
                    if (customer is null)
                        return Results.NotFound("العميل غير موجود.");

                    var payment = new CustomerPayment
                    {
                        CustomerId = customer.Id,
                        Amount = input.Amount,
                        Method = string.IsNullOrWhiteSpace(input.Method) ? "كاش" : input.Method!,
                        Note = input.Note?.Trim(),
                        PaymentDate = input.PaymentDate ?? DateTime.UtcNow
                    };

                    db.CustomerPayments.Add(payment);
                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        success = true,
                        id = payment.Id,
                        customerId = payment.CustomerId,
                        amount = payment.Amount,
                        method = payment.Method,
                        note = payment.Note,
                        paymentDate = payment.PaymentDate
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest("خطأ أثناء حفظ الدفعة: " + ex.Message);
                }
            });

            // Edit payment
            app.MapPut("/api/customer-payments/{paymentId:int}", async (AppDbContext db, int paymentId, CustomerPaymentInput input) =>
            {
                try
                {
                    if (input == null)
                        return Results.BadRequest("بيانات الدفع غير صحيحة.");

                    if (input.Amount <= 0)
                        return Results.BadRequest("المبلغ يجب أن يكون أكبر من صفر.");

                    var payment = await db.CustomerPayments.FindAsync(paymentId);
                    if (payment is null)
                        return Results.NotFound("الدفعة غير موجودة.");

                    payment.Amount = input.Amount;
                    payment.Method = string.IsNullOrWhiteSpace(input.Method) ? "كاش" : input.Method!;
                    payment.Note = input.Note?.Trim();
                    payment.PaymentDate = input.PaymentDate ?? payment.PaymentDate;

                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        success = true,
                        id = payment.Id,
                        customerId = payment.CustomerId,
                        amount = payment.Amount,
                        method = payment.Method,
                        note = payment.Note,
                        paymentDate = payment.PaymentDate
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest("خطأ أثناء تعديل الدفعة: " + ex.Message);
                }
            });

            // Delete payment
            app.MapDelete("/api/customer-payments/{paymentId:int}", async (AppDbContext db, int paymentId) =>
            {
                var payment = await db.CustomerPayments.FindAsync(paymentId);
                if (payment is null) return Results.NotFound();

                db.CustomerPayments.Remove(payment);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });

            app.Run();
        }
    }
}
