
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

