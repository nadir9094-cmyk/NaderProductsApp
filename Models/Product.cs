using System;

namespace NaderProductsApp.Models
{
    public class Product
    {
        public int Id { get; set; }

        public string Barcode { get; set; } = "";
        public string Name { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public string Category { get; set; } = "";

        public int Quantity { get; set; }
        public int MinQuantity { get; set; }
        public int SoldQuantity { get; set; }

        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }

        public bool IsVatIncluded { get; set; } = true;

        public DateTime? ExpiryDate { get; set; }

        // بيانات العرض
        public bool OfferEnabled { get; set; }
        public string? OfferName { get; set; }
        public decimal? OfferPrice { get; set; }
        public DateTime? OfferStart { get; set; }
        public DateTime? OfferEnd { get; set; }
        public bool? OfferVatIncluded { get; set; }

        // كمية متبقية (محسوبة)
        public int RemainingQuantity => Quantity - SoldQuantity;
    }
}
