using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class FixDbSchema
{
    public static void Apply(AppDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        // PostgreSQL
        if (provider.Contains("Npgsql"))
        {
            // أضف العمود لو غير موجود
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE "CashierInvoices"
                ADD COLUMN IF NOT EXISTS "CashierId" integer NOT NULL DEFAULT 0;
            """);
        }
        // SQLite (احتياط لو رجعت له محليًا)
        else if (provider.Contains("Sqlite"))
        {
            try
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE CashierInvoices ADD COLUMN CashierId INTEGER NOT NULL DEFAULT 0;");
            }
            catch { /* لو موجود تجاهل */ }
        }
    }
}
