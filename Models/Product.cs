
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

    public decimal PurchasePrice { get; set; } = 0m;
    public decimal SalePrice { get; set; } = 0m;

    public bool IsVatIncluded { get; set; } = true;

    // بدل نص: نخزن تاريخ فعلي (DATE)
    public DateTime? ExpiryDate { get; set; }

    public bool OfferEnabled { get; set; } = false;
    public string? OfferName { get; set; }
    public decimal? OfferPrice { get; set; }
    public bool OfferVatIncluded { get; set; } = true;
    public DateTime? OfferStart { get; set; }
    public DateTime? OfferEnd { get; set; }
}

