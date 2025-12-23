cd C:\sami; $ErrorActionPreference='Stop';

$stamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
$bakDir = "C:\sami\_bak_$stamp"; New-Item -ItemType Directory -Force -Path $bakDir | Out-Null

function Backup-File([string]$p){
  if(Test-Path $p){
    Copy-Item $p (Join-Path $bakDir (Split-Path $p -Leaf)) -Force
  }
}

# 0) نسخ احتياطي للملفات اللي بنعدلها
Backup-File ".\Program.cs"
Backup-File ".\Data\AppDbContext.cs"
Backup-File ".\Models\Product.cs"
Backup-File ".\Models\CashierInvoice.cs"
Backup-File ".\Models\CashierInvoiceItem.cs"
Backup-File ".\BackupApiV2.cs"

# 1) تعديل الموديلات: double -> decimal + تواريخ string -> DateTime?
@'
using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class Product
{
    public int Id { get; set; }

    [Required] public string Barcode { get; set; } = "";
    [Required] public string Name { get; set; } = "";

    public string? SupplierName { get; set; }
    public string? Category { get; set; }

    public int Quantity { get; set; } = 0;
    public int MinQuantity { get; set; } = 0;
    public int SoldQuantity { get; set; } = 0;

    public decimal PurchasePrice { get; set; } = 0m;
    public decimal SalePrice { get; set; } = 0m;

    public bool IsVatIncluded { get; set; } = true;

    // بدل نص: نخزن تاريخ فعلي (DATE)
    public DateTime? ExpiryDate { get; set; }

    public bool OfferEnabled { get; set; } = false;
    public string? OfferName { get; set; }
    public decimal? OfferPrice { get; set; }
    public bool OfferVatIncluded { get; set; } = true;
    public DateTime? OfferStart { get; set; }
    public DateTime? OfferEnd { get; set; }
}
'@ | Set-Content -Encoding UTF8 ".\Models\Product.cs"

@'
namespace NaderProductsApp.Models;

public class CashierInvoice
{
    public int Id { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public string PaymentMethod { get; set; } = "cash"; // cash/card/deferred
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    public bool IsSuspended { get; set; } = false;
    public string? Notes { get; set; }

    // مبالغ مالية: لازم Decimal
    public decimal SubTotal { get; set; } = 0m;
    public decimal VatTotal { get; set; } = 0m;
    public decimal DiscountTotal { get; set; } = 0m;
    public decimal GrandTotal { get; set; } = 0m;

    public decimal ReturnAmount { get; set; } = 0m;

    public List<CashierInvoiceItem> Items { get; set; } = new();
}
'@ | Set-Content -Encoding UTF8 ".\Models\CashierInvoice.cs"

@'
namespace NaderProductsApp.Models;

public class CashierInvoiceItem
{
    public int Id { get; set; }

    public int CashierInvoiceId { get; set; }
    public CashierInvoice? CashierInvoice { get; set; }

    public int? ProductId { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = "";

    // كمية/سعر/خصم: Decimal أفضل (حتى لو صار فيه وزن/كسور)
    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal Discount { get; set; } = 0m;

    public bool TaxIncluded { get; set; } = true;

    public bool HasOffer { get; set; } = false;
    public string? OfferName { get; set; }
}
'@ | Set-Content -Encoding UTF8 ".\Models\CashierInvoiceItem.cs"

# 2) إعادة كتابة AppDbContext بشكل نظيف + فهارس + أنواع أعمدة
@'
using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Models;

namespace NaderProductsApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

    public DbSet<CashierInvoice> CashierInvoices => Set<CashierInvoice>();
    public DbSet<CashierInvoiceItem> CashierInvoiceItems => Set<CashierInvoiceItem>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<NaderProductsApp.Models.AppSetting> AppSettings { get; set; } = null!;
    public DbSet<NaderProductsApp.Models.SuppliersStoreRow> SuppliersStore { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Keys (for tables added earlier)
        modelBuilder.Entity<NaderProductsApp.Models.AppSetting>().HasKey(x => x.Id);
        modelBuilder.Entity<NaderProductsApp.Models.SuppliersStoreRow>().HasKey(x => x.Id);

        // Relationships
        modelBuilder.Entity<CashierInvoiceItem>(e =>
        {
            e.HasOne(x => x.CashierInvoice)
             .WithMany(x => x.Items)
             .HasForeignKey(x => x.CashierInvoiceId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.CashierInvoiceId);
            e.HasIndex(x => x.Barcode);
        });

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

        // Indexes / constraints
        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Barcode).IsUnique();
            e.HasIndex(x => x.Name);
            e.Property(x => x.PurchasePrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.SalePrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.OfferPrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.ExpiryDate).HasColumnType("date");
            e.Property(x => x.OfferStart).HasColumnType("date");
            e.Property(x => x.OfferEnd).HasColumnType("date");
        });

        modelBuilder.Entity<CashierInvoice>(e =>
        {
            e.HasIndex(x => x.InvoiceDate);
            e.HasIndex(x => x.CustomerId);
            e.Property(x => x.SubTotal).HasColumnType("numeric(18,2)");
            e.Property(x => x.VatTotal).HasColumnType("numeric(18,2)");
            e.Property(x => x.DiscountTotal).HasColumnType("numeric(18,2)");
            e.Property(x => x.GrandTotal).HasColumnType("numeric(18,2)");
            e.Property(x => x.ReturnAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<CashierInvoiceItem>(e =>
        {
            e.Property(x => x.Quantity).HasColumnType("numeric(18,3)");
            e.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.Discount).HasColumnType("numeric(18,2)");
        });
    }
}
'@ | Set-Content -Encoding UTF8 ".\Data\AppDbContext.cs"

# 3) Program.cs: EnsureCreated -> Migrate
$prog = Get-Content ".\Program.cs" -Raw -Encoding UTF8
$prog = $prog -replace 'db\.Database\.EnsureCreated\(\)\s*;', 'db.Database.Migrate();'
Set-Content -Encoding UTF8 ".\Program.cs" $prog

# 4) إصلاح Backup restore: تنظيف transaction_timeout قبل تنفيذ psql
$bk = Get-Content ".\BackupApiV2.cs" -Raw -Encoding UTF8
if($bk -notmatch "SanitizeSqlForRestore"){
  $insert = @"

    private static string SanitizeSqlForRestore(string file)
    {
        // Remove config lines that may not exist on older PostgreSQL versions
        // e.g. SET transaction_timeout = ...
        var tmp = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + "_SANITIZED.sql");
        var lines = File.ReadAllLines(file);
        using var w = new StreamWriter(tmp, false);
        foreach (var line in lines)
        {
            var t = line.TrimStart();
            if (t.StartsWith("SET transaction_timeout", StringComparison.OrdinalIgnoreCase)) continue;
            w.WriteLine(line);
        }
        return tmp;
    }
"@

  # add function near ResolveConnectionString (safe)
  $bk = $bk -replace "(private static string\?\s+ResolveConnectionString[\s\S]*?\}\s*\n)", "`$1$insert`n"
  # use sanitize in restore
  $bk = $bk -replace "arguments:\s*\$\"-h \\\\\"{b\.Host}\\\\\" -p {b\.Port} -U \\\\\"{b\.Username}\\\\\" -d \\\\\"{b\.Database}\\\\\" -v ON_ERROR_STOP=1 -f \\\\\"{file}\\\\\"\"",
                      "var sanitized = SanitizeSqlForRestore(file);`n            arguments: $""-h \""{b.Host}\"" -p {b.Port} -U \""{b.Username}\"" -d \""{b.Database}\"" -v ON_ERROR_STOP=1 -f \""{sanitized}\"""""
  Set-Content -Encoding UTF8 ".\BackupApiV2.cs" $bk
}

# 5) إعادة إنشاء قاعدة البيانات (بداية نظيفة)
# يحتاج psql موجود في PATH (واضح عندك لأنه يشتغل وقت الاستعادة)
$cs = (Get-Content ".\appsettings.json" -Raw -Encoding UTF8) | ConvertFrom-Json
$dbName = $cs.ConnectionStrings.Default -replace '.*Database=([^;]+).*','$1'
if([string]::IsNullOrWhiteSpace($dbName)){ $dbName="naderposdb" }

Write-Host "Recreating database: $dbName" -ForegroundColor Yellow
# نغلق الاتصالات ثم نحذف وننشئ
psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$dbName';" | Out-Null
psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS $dbName;" | Out-Null
psql -U postgres -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $dbName;" | Out-Null

# 6) تنظيف migrations القديمة وإعادة توليدها
if(Test-Path ".\Migrations"){ Remove-Item ".\Migrations" -Recurse -Force }
dotnet tool update --global dotnet-ef | Out-Null
dotnet restore | Out-Null

dotnet ef migrations add Initial_Postgres_Clean -o Migrations
dotnet ef database update

Write-Host "`nDONE ✅ شغّل البرنامج:" -ForegroundColor Green
Write-Host "dotnet run" -ForegroundColor Cyan
Write-Host "`nBackup of old files is here: $bakDir" -ForegroundColor DarkGray

