using System.ComponentModel.DataAnnotations;

namespace NaderProductsApp.Models;

public class Product
{
    public int Id { get; set; }

    [Required] public string Barcode { get; set; } = "";
    [Required] public string Name { get; set; } = "";

    public string? SupplierName { get; set; }
    public string? Category { get; set; }

    public int Quantity { get; set; } = 0;
    public int MinQuantity { get; set; } = 0;
    public int SoldQuantity { get; set; } = 0;

    public double PurchasePrice { get; set; } = 0;
    public double SalePrice { get; set; } = 0;

    public bool IsVatIncluded { get; set; } = true;

    // نخزنها كنص (YYYY-MM-DD) مثل الشاشة
    public string? ExpiryDate { get; set; }

    public bool OfferEnabled { get; set; } = false;
    public string? OfferName { get; set; }
    public double? OfferPrice { get; set; }
    public bool OfferVatIncluded { get; set; } = true;
    public string? OfferStart { get; set; }
    public string? OfferEnd { get; set; }
}
