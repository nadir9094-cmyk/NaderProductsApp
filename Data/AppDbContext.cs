using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Models;

namespace NaderProductsApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
        public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Product configuration
            var product = modelBuilder.Entity<Product>();

            // تخزين تاريخ الصلاحية كـ date فقط لتفادي مشاكل التوقيت في PostgreSQL
            product.Property(p => p.ExpiryDate)
                   .HasColumnType("date");

            // Customers
            var customer = modelBuilder.Entity<Customer>();
            customer.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(200);

            customer.Property(c => c.Status)
                    .IsRequired()
                    .HasMaxLength(32);

            // Invoices
            var invoice = modelBuilder.Entity<CustomerInvoice>();
            invoice.Property(i => i.Description)
                   .HasMaxLength(500);

            invoice.HasOne(i => i.Customer)
                   .WithMany(c => c.Invoices)
                   .HasForeignKey(i => i.CustomerId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Payments
            var payment = modelBuilder.Entity<CustomerPayment>();
            payment.Property(p => p.Method)
                   .HasMaxLength(50);

            payment.HasOne(p => p.Customer)
                   .WithMany(c => c.Payments)
                   .HasForeignKey(p => p.CustomerId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
