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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var product = modelBuilder.Entity<Product>();

            // تاريخ انتهاء الصلاحية = date فقط (بدون وقت)
            product.Property(p => p.ExpiryDate)
                   .HasColumnType("date");

            // تواريخ العرض = timestamp بدون time zone
            product.Property(p => p.OfferStart)
                   .HasColumnType("timestamp without time zone");

            product.Property(p => p.OfferEnd)
                   .HasColumnType("timestamp without time zone");
        }
    }
}
