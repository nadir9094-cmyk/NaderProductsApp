$ErrorActionPreference="Stop"
cd C:\sami

Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

if(!(Test-Path ".\Program.cs")){ throw "Program.cs_NOT_FOUND" }
if(!(Test-Path ".\Data\AppDbContext.cs")){ throw "AppDbContext_NOT_FOUND" }
if(!(Test-Path ".\Models\Product.cs")){ throw "Product.cs_NOT_FOUND" }

# ===== اكتب موديلات العملاء =====
New-Item -ItemType Directory -Force .\Models | Out-Null

@"
using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class Customer
{
    public int Id { get; set; }

    [Required] public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public string Status { get; set; } = "active";

    public List<CustomerInvoice> Invoices { get; set; } = new();
    public List<CustomerPayment> Payments { get; set; } = new();
}
"@ | Set-Content .\Models\Customer.cs -Encoding UTF8

@"
using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class CustomerInvoice
{
    public int Id { get; set; }
    [Required] public int CustomerId { get; set; }

    public double Amount { get; set; } = 0;
    public string Description { get; set; } = "تعديل رصيد يدوي";
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

    public Customer? Customer { get; set; }
}
"@ | Set-Content .\Models\CustomerInvoice.cs -Encoding UTF8

@"
using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class CustomerPayment
{
    public int Id { get; set; }
    [Required] public int CustomerId { get; set; }

    public double Amount { get; set; } = 0;
    public string Method { get; set; } = "كاش";
    public string? Note { get; set; }
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

    public Customer? Customer { get; set; }
}
"@ | Set-Content .\Models\CustomerPayment.cs -Encoding UTF8

# ===== حدّث DbContext (إضافة DbSets فقط بدون تخريب الباقي) =====
$ctx = Get-Content .\Data\AppDbContext.cs -Raw

if($ctx -notmatch 'DbSet<Customer>\s+Customers'){
  $ctx = $ctx -replace 'public DbSet<Product> Products => Set<Product>();', @"
public DbSet<Product> Products => Set<Product>();
public DbSet<Customer> Customers => Set<Customer>();
public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
"@
}

# أضف العلاقات داخل OnModelCreating أو أنشئها لو ما هي موجودة
if($ctx -notmatch 'OnModelCreating'){
  $ctx = $ctx -replace '\}\s*$', @"

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>()
            .HasMany(x => x.Invoices)
            .WithOne(x => x.Customer!)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Customer>()
            .HasMany(x => x.Payments)
            .WithOne(x => x.Customer!)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
"@
} elseif($ctx -notmatch 'modelBuilder\.Entity<Customer>'){
  $ctx = $ctx -replace 'protected override void OnModelCreating\(ModelBuilder modelBuilder\)\s*\{', @"
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Customer>()
        .HasMany(x => x.Invoices)
        .WithOne(x => x.Customer!)
        .HasForeignKey(x => x.CustomerId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Customer>()
        .HasMany(x => x.Payments)
        .WithOne(x => x.Customer!)
        .HasForeignKey(x => x.CustomerId)
        .OnDelete(DeleteBehavior.Cascade);

"@
}

Set-Content .\Data\AppDbContext.cs -Encoding UTF8 -Value $ctx

# ===== حدّث Program.cs (أضف endpoints العملاء قبل app.Run) =====
$prog = Get-Content .\Program.cs -Raw

if($prog -notmatch 'using Microsoft\.AspNetCore\.Mvc;'){
  $prog = $prog -replace 'using Microsoft\.EntityFrameworkCore;\s*', "using Microsoft.EntityFrameworkCore;`r`nusing Microsoft.AspNetCore.Mvc;`r`n"
}
if($prog -notmatch 'using NaderProductsApp\.Models;'){
  $prog = $prog -replace 'using NaderProductsApp\.Data;\s*', "using NaderProductsApp.Data;`r`nusing NaderProductsApp.Models;`r`n"
}

# احذف أي Placeholder قديم لـ /api/customers/full
$prog = [regex]::Replace($prog, '(?s)app\.MapGet\(\"/api/customers/full\".*?\);\s*', '')

$block = @"
app.MapGet(""/api/customers/full"", async ([FromServices] AppDbContext db) =>
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
        invoices = (c.Invoices ?? new List<CustomerInvoice>()).OrderByDescending(x=>x.Id).Select(x => new {
            id = x.Id, amount = x.Amount, description = x.Description, date = x.Date
        }),
        payments = (c.Payments ?? new List<CustomerPayment>()).OrderByDescending(x=>x.Id).Select(x => new {
            id = x.Id, amount = x.Amount, method = x.Method, note = x.Note, date = x.Date
        })
    }));
});

app.MapPost(""/api/customers"", async ([FromServices] AppDbContext db, Customer input) =>
{
    input.Name = (input.Name ?? """").Trim();
    if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest(""REQUIRED_NAME"");

    input.Phone = (input.Phone ?? """").Trim();
    input.Address = (input.Address ?? """").Trim();
    input.Notes = (input.Notes ?? """").Trim();
    input.Status = string.IsNullOrWhiteSpace(input.Status) ? ""active"" : input.Status;

    db.Customers.Add(input);
    await db.SaveChangesAsync();
    return Results.Ok(new { id = input.Id });
});
"@

if($prog -notmatch 'app\.Run\('){ throw "APP_RUN_NOT_FOUND" }
$prog = $prog -replace '(\s*app\.Run\([^\)]*\);\s*)', "`r`n$block`r`n`$1"
Set-Content .\Program.cs -Encoding UTF8 -Value $prog

# إعادة إنشاء القاعدة (لإضافة جداول العملاء) بدون ترقيع migrations
if(Test-Path .\naderpos.db){
  $bak = Join-Path $env:TEMP ("naderpos_before_customers_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".db")
  Copy-Item .\naderpos.db $bak -Force
  Remove-Item .\naderpos.db -Force
  Write-Host ("DB_BACKUP: " + $bak)
}

dotnet build
dotnet run --urls "http://127.0.0.1:5050"
