namespace NaderProductsApp.Models;

public class AppSetting
{
    public int Id { get; set; } = 1;

    public string? StoreName { get; set; }
    public string? CommercialRegister { get; set; }
    public string? VatNumber { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }

    public string? InvoicePaper { get; set; }           // "Cashier" أو "A4"
    public int? CashierPaperWidthMm { get; set; }       // 80 افتراضي

    public string? StoreLogoBase64 { get; set; }
    public string? InvoiceFooterNotes { get; set; }

    public string? BarcodeType { get; set; }            // "QR"
    public int? ZatcaPhase { get; set; }                // 1 أو 2

    public string? Phase2InvoiceHash { get; set; }
    public string? Phase2Signature { get; set; }
    public string? Phase2PublicKey { get; set; }
    public string? Phase2CertificateSignature { get; set; }
}
