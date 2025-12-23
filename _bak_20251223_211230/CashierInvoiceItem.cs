namespace NaderProductsApp.Models;

public class CashierInvoiceItem
{
    public int Id { get; set; }

    public int CashierInvoiceId { get; set; }
    public CashierInvoice? CashierInvoice { get; set; }

    public int? ProductId { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = "";

    public double Quantity { get; set; } = 0;
    public double UnitPrice { get; set; } = 0;
    public double Discount { get; set; } = 0;

    public bool TaxIncluded { get; set; } = true;

    public bool HasOffer { get; set; } = false;
    public string? OfferName { get; set; }
}
