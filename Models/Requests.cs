namespace NaderProductsApp.Models;

// =====================
// CUSTOMERS
// =====================
public sealed class CustomerLedgerRequest
{
    public double Amount { get; set; }
    public string? Description { get; set; }
    public string? Date { get; set; }        // optional
    public string? InvoiceDate { get; set; } // optional alias
}

public sealed class CustomerPaymentRequest
{
    public double Amount { get; set; }
    public string? Method { get; set; }
    public string? Note { get; set; }
    public string? Date { get; set; }         // optional
    public string? PaymentDate { get; set; }  // alias used by UI sometimes
}

// =====================
// CASHIER
// =====================
public sealed class CashierInvoiceItemRequest
{
    public int ProductId { get; set; }

    // main naming
    public string? Name { get; set; }
    public string? Barcode { get; set; }

    public double UnitPrice { get; set; }
    public double Quantity { get; set; }
    public double LineTotal { get; set; }

    // VAT/Offer
    public bool IsVatIncluded { get; set; }
    public bool OfferEnabled { get; set; }
    public string? OfferName { get; set; }
    public double? OfferPrice { get; set; }
    public bool OfferVatIncluded { get; set; }

    // ===== aliases (Program.cs currently expects these names) =====
    public string? ProductName { get; set; }   // alias for Name
    public double Discount { get; set; }       // line discount if UI sends it
    public bool TaxIncluded { get; set; }      // alias for IsVatIncluded
    public bool HasOffer { get; set; }         // alias for OfferEnabled
}

public sealed class CashierInvoiceRequest
{
    public string? InvoiceDate { get; set; }
    public string? PaymentMethod { get; set; }

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public double SubTotal { get; set; }
    public double VatTotal { get; set; }
    public double DiscountTotal { get; set; }
    public double GrandTotal { get; set; }

    public string? Notes { get; set; }
    public bool IsSuspended { get; set; }

    public List<CashierInvoiceItemRequest> Items { get; set; } = new();
}

// =====================
// RETURNS
// =====================
public sealed class ReturnItemRequest
{
    public int ItemId { get; set; }
    public int ProductId { get; set; }
    public double ReturnQuantity { get; set; }
}

public sealed class ReturnRequest
{
    public string? ReturnDate { get; set; }
    public string? Note { get; set; }
    public List<ReturnItemRequest> Items { get; set; } = new();
}
