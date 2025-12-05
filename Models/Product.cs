namespace NaderProductsApp.Models;

public class Product
{
    public int Id { get; set; }

    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    public int SoldQuantity { get; set; }

    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public bool IsVatIncluded { get; set; } = true;

    public DateTime? ExpiryDate { get; set; }

    public bool OfferEnabled { get; set; }
    public DateTime? OfferStart { get; set; }
    public DateTime? OfferEnd { get; set; }
    public decimal? OfferPrice { get; set; }

    public int RemainingQuantity => Quantity - SoldQuantity;
}
