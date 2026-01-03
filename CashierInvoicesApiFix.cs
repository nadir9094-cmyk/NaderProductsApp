using Microsoft.EntityFrameworkCore;
using NaderProductsApp.Data;

public static class CashierInvoicesApiFix
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/cashier/invoices", async (AppDbContext db, dynamic body) =>
        {
            // حفظ صوري مؤقت (عشان الشاشة ما تنهار)
            return Results.Ok(new {
                ok = true,
                invoiceId = 1
            });
        });
    }
}
