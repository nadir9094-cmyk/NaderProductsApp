using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Models;
using NaderProductsApp.Models.ControlPanel;

namespace NaderProductsApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Core
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

    public DbSet<CashierInvoice> CashierInvoices => Set<CashierInvoice>();
    public DbSet<CashierInvoiceItem> CashierInvoiceItems => Set<CashierInvoiceItem>();

    public DbSet<Expense> Expenses => Set<Expense>();

    // Employees
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeSession> EmployeeSessions => Set<EmployeeSession>();

    // App Settings / Suppliers Store
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<SuppliersStoreRow> SuppliersStore => Set<SuppliersStoreRow>();

    // Control Panel (Super Admin)
    public DbSet<CpAdminUser> CpAdminUsers => Set<CpAdminUser>();
    public DbSet<CpAdminSession> CpAdminSessions => Set<CpAdminSession>();
    public DbSet<CpPlan> CpPlans => Set<CpPlan>();
    public DbSet<CpTenant> CpTenants => Set<CpTenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ===== Control Panel =====
        modelBuilder.Entity<CpAdminUser>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<CpAdminSession>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.AdminUserId);
        });

        modelBuilder.Entity<CpPlan>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.PriceMonthly).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<CpTenant>(e =>
        {
            e.HasIndex(x => x.TenantCode).IsUnique();
            e.HasIndex(x => x.PlanId);
        });

        // ===== Keys =====
        modelBuilder.Entity<AppSetting>().HasKey(x => x.Id);
        modelBuilder.Entity<SuppliersStoreRow>().HasKey(x => x.Id);

        // ===== Products =====
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

        // ===== Customers =====
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

        // ===== Cashier =====
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
            e.HasOne(x => x.CashierInvoice)
             .WithMany(x => x.Items)
             .HasForeignKey(x => x.CashierInvoiceId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.CashierInvoiceId);
            e.HasIndex(x => x.Barcode);

            e.Property(x => x.Quantity).HasColumnType("numeric(18,3)");
            e.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.Discount).HasColumnType("numeric(18,2)");
        });

        // ===== Employees =====
        modelBuilder.Entity<Employee>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Permissions).HasColumnType("bigint");
        });

        modelBuilder.Entity<EmployeeSession>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.Employee)
             .WithMany()
             .HasForeignKey(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
