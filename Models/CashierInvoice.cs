
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
    public int? CashierId { get; set; }
    public string? CashierName { get; set; }
}


