using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Models;

namespace NaderProductsApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
}
