namespace NaderProductsApp.Models;

public class SettingsDto
{
    public string? StoreName { get; set; }
    public string? CommercialRegister { get; set; }
    public string? VatNumber { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }

    public string? InvoicePaper { get; set; }
    public int CashierPaperWidthMm { get; set; } = 80;

    public string? StoreLogoBase64 { get; set; }
    public string? InvoiceFooterNotes { get; set; }

    public string? BarcodeType { get; set; }
    public int ZatcaPhase { get; set; } = 1;

    public string? Phase2InvoiceHash { get; set; }
    public string? Phase2Signature { get; set; }
    public string? Phase2PublicKey { get; set; }
    public string? Phase2CertificateSignature { get; set; }
}
