namespace NaderProductsApp.Models;

public class CashierInvoiceItem
{
    public int Id { get; set; }

    public int CashierInvoiceId { get; set; }
    public CashierInvoice? CashierInvoice { get; set; }

    public int? ProductId { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = "";

    // كمية/سعر/خصم: Decimal أفضل (حتى لو صار فيه وزن/كسور)
    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal Discount { get; set; } = 0m;

    public bool TaxIncluded { get; set; } = true;

    public bool HasOffer { get; set; } = false;
    public string? OfferName { get; set; }
}
