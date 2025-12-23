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

    public double SubTotal { get; set; } = 0;
    public double VatTotal { get; set; } = 0;
    public double DiscountTotal { get; set; } = 0;
    public double GrandTotal { get; set; } = 0;

    public double ReturnAmount { get; set; } = 0;

    public List<CashierInvoiceItem> Items { get; set; } = new();
}

