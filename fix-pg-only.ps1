param()

$root = "C:\sami"
$prog = Join-Path $root "Program.cs"
$dbctx = Join-Path $root "Data\AppDbContext.cs"
$modelsDir = Join-Path $root "Models"

if(!(Test-Path $prog)){ throw "Program.cs not found: $prog" }

$stamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
Copy-Item $prog "$prog.before_pgonlyfix_$stamp.bak" -Force

# ---------- Patch Program.cs ----------
$p = Get-Content $prog -Raw -Encoding UTF8

# remove sqlite using (if any)
$p = [regex]::Replace($p, '(?m)^\s*using\s+Microsoft\.Data\.Sqlite\s*;\s*\r?\n', '')

# remove the two persisted blocks completely (they contain broken strings now)
$p = [regex]::Replace(
  $p,
  '(?s)//\s*=======================\s*SUPPLIERS STORE \(PERSISTED\)\s*=======================.*?//\s*END_SUPPLIERS_STORE\s*',
  ''
)
$p = [regex]::Replace(
  $p,
  '(?s)//\s*=======================\s*SETTINGS \(PERSISTED\)\s*=======================.*?//\s*END_SETTINGS\s*',
  ''
)

# remove any leftover endpoints for these routes (safety)
$p = [regex]::Replace($p, '(?is)app\.Map(?:Get|Put|Post|Delete|Patch)\(\s*"/api/settings"\s*,.*?\)\s*;\s*', '')
$p = [regex]::Replace($p, '(?is)app\.Map(?:Get|Put|Post|Delete|Patch)\(\s*"/api/suppliers-store"\s*,.*?\)\s*;\s*', '')

# Postgres/EF endpoints block
$pgBlock = @"
#region POSTGRES_ONLY_SETTINGS_SUPPLIERS (EF Core)
const string DefaultSuppliersJson = @""{""""suppliers"""":[],""""invoices"""":[],""""payments"""":[]}""";

static async Task<NaderProductsApp.Models.AppSetting> EnsureSettingsRow(AppDbContext db)
{
    var row = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == 1);
    if (row is null)
    {
        row = new NaderProductsApp.Models.AppSetting
        {
            Id = 1,
            InvoicePaper = "Cashier",
            CashierPaperWidthMm = 80,
            BarcodeType = "QR",
            ZatcaPhase = 1
        };
        db.AppSettings.Add(row);
        await db.SaveChangesAsync();
    }
    return row;
}

static async Task<NaderProductsApp.Models.SuppliersStoreRow> EnsureSuppliersStoreRow(AppDbContext db)
{
    var row = await db.SuppliersStore.FirstOrDefaultAsync(x => x.Id == 1);
    if (row is null)
    {
        row = new NaderProductsApp.Models.SuppliersStoreRow { Id = 1, Json = DefaultSuppliersJson };
        db.SuppliersStore.Add(row);
        await db.SaveChangesAsync();
    }
    if (string.IsNullOrWhiteSpace(row.Json)) row.Json = DefaultSuppliersJson;
    return row;
}

app.MapGet("/api/settings", async (AppDbContext db) =>
{
    var row = await EnsureSettingsRow(db);
    return Results.Ok(new
    {
        storeName = row.StoreName ?? "",
        commercialRegister = row.CommercialRegister ?? "",
        vatNumber = row.VatNumber ?? "",
        storeAddress = row.StoreAddress ?? "",
        storePhone = row.StorePhone ?? "",
        invoicePaper = string.IsNullOrWhiteSpace(row.InvoicePaper) ? "Cashier" : row.InvoicePaper,
        cashierPaperWidthMm = row.CashierPaperWidthMm ?? 80,
        storeLogoBase64 = row.StoreLogoBase64 ?? "",
        invoiceFooterNotes = row.InvoiceFooterNotes ?? "",
        barcodeType = string.IsNullOrWhiteSpace(row.BarcodeType) ? "QR" : row.BarcodeType,
        zatcaPhase = row.ZatcaPhase ?? 1,
        phase2InvoiceHash = row.Phase2InvoiceHash ?? "",
        phase2Signature = row.Phase2Signature ?? "",
        phase2PublicKey = row.Phase2PublicKey ?? "",
        phase2CertificateSignature = row.Phase2CertificateSignature ?? ""
    });
});

app.MapPut("/api/settings", async (AppDbContext db, HttpRequest request) =>
{
    var dto = await request.ReadFromJsonAsync<NaderProductsApp.Models.SettingsDto>();
    if (dto is null) return Results.BadRequest("INVALID_BODY");

    var row = await EnsureSettingsRow(db);

    var invoicePaper = (dto.InvoicePaper ?? "Cashier").Trim();
    if (invoicePaper != "A4" && invoicePaper != "Cashier") invoicePaper = "Cashier";

    var barcodeType = (dto.BarcodeType ?? "QR").Trim().ToUpperInvariant();
    if (barcodeType != "QR") barcodeType = "QR";

    var phase = dto.ZatcaPhase == 2 ? 2 : 1;

    var width = dto.CashierPaperWidthMm <= 0 ? 80 : dto.CashierPaperWidthMm;
    if (width < 58) width = 58;
    if (width > 120) width = 120;

    var logo = (dto.StoreLogoBase64 ?? "").Trim();
    if (logo.Length > 2_000_000) return Results.BadRequest("LOGO_TOO_LARGE");

    row.StoreName = (dto.StoreName ?? "").Trim();
    row.CommercialRegister = (dto.CommercialRegister ?? "").Trim();
    row.VatNumber = (dto.VatNumber ?? "").Trim();
    row.StoreAddress = (dto.StoreAddress ?? "").Trim();
    row.StorePhone = (dto.StorePhone ?? "").Trim();

    row.InvoicePaper = invoicePaper;
    row.CashierPaperWidthMm = width;
    row.StoreLogoBase64 = logo;
    row.InvoiceFooterNotes = (dto.InvoiceFooterNotes ?? "").Trim();

    row.BarcodeType = barcodeType;
    row.ZatcaPhase = phase;

    row.Phase2InvoiceHash = phase == 2 ? (dto.Phase2InvoiceHash ?? "").Trim() : "";
    row.Phase2Signature = phase == 2 ? (dto.Phase2Signature ?? "").Trim() : "";
    row.Phase2PublicKey = phase == 2 ? (dto.Phase2PublicKey ?? "").Trim() : "";
    row.Phase2CertificateSignature = phase == 2 ? (dto.Phase2CertificateSignature ?? "").Trim() : "";

    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/suppliers-store", async (AppDbContext db) =>
{
    var row = await EnsureSuppliersStoreRow(db);
    return Results.Text(row.Json ?? DefaultSuppliersJson, "application/json; charset=utf-8");
});

app.MapPut("/api/suppliers-store", async (AppDbContext db, HttpRequest request) =>
{
    using var sr = new StreamReader(request.Body);
    var raw = await sr.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(raw)) return Results.BadRequest("EMPTY_BODY");

    var row = await EnsureSuppliersStoreRow(db);
    row.Json = raw;
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});
#endregion
"@

# insert block before first app.Run / app.RunAsync
if($p -match '(?m)^\s*(await\s+)?app\.Run(?:Async)?\s*\('){
  $p = [regex]::Replace($p, '(?m)^\s*(await\s+)?app\.Run(?:Async)?\s*\(.*$', $pgBlock + "`r`n`r`n" + '$0', 1)
} else {
  $p = $p + "`r`n`r`n" + $pgBlock
}

Set-Content -Encoding UTF8 -Path $prog -Value $p

# ---------- Ensure Models ----------
if(!(Test-Path $modelsDir)){ New-Item -ItemType Directory -Path $modelsDir | Out-Null }

$as = Join-Path $modelsDir "AppSetting.cs"
$ss = Join-Path $modelsDir "SuppliersStoreRow.cs"
$sd = Join-Path $modelsDir "SettingsDto.cs"

if(!(Test-Path $as)){
@"
namespace NaderProductsApp.Models;

public class AppSetting
{
    public int Id { get; set; } = 1;

    public string? StoreName { get; set; }
    public string? CommercialRegister { get; set; }
    public string? VatNumber { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }

    public string? InvoicePaper { get; set; }           // Cashier/A4
    public int? CashierPaperWidthMm { get; set; }       // default 80

    public string? StoreLogoBase64 { get; set; }
    public string? InvoiceFooterNotes { get; set; }

    public string? BarcodeType { get; set; }            // QR
    public int? ZatcaPhase { get; set; }                // 1/2

    public string? Phase2InvoiceHash { get; set; }
    public string? Phase2Signature { get; set; }
    public string? Phase2PublicKey { get; set; }
    public string? Phase2CertificateSignature { get; set; }
}
"@ | Set-Content -Encoding UTF8 -Path $as
}

if(!(Test-Path $ss)){
@"
namespace NaderProductsApp.Models;

public class SuppliersStoreRow
{
    public int Id { get; set; } = 1;
    public string? Json { get; set; }
}
"@ | Set-Content -Encoding UTF8 -Path $ss
}

if(!(Test-Path $sd)){
@"
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
"@ | Set-Content -Encoding UTF8 -Path $sd
}

# ---------- Patch AppDbContext (if exists) ----------
if(Test-Path $dbctx){
  $c = Get-Content $dbctx -Raw -Encoding UTF8

  if($c -notmatch 'DbSet\s*<\s*AppSetting\s*>\s*AppSettings'){
    $c = [regex]::Replace($c, '(?m)^\s*public\s+class\s+AppDbContext\s*:\s*DbContext\s*\{',
      '$0' + "`r`n    public DbSet<NaderProductsApp.Models.AppSetting> AppSettings { get; set; } = null!;", 1)
  }
  if($c -notmatch 'DbSet\s*<\s*SuppliersStoreRow\s*>\s*SuppliersStore'){
    $c = [regex]::Replace($c, '(?m)^\s*public\s+class\s+AppDbContext\s*:\s*DbContext\s*\{',
      '$0' + "`r`n    public DbSet<NaderProductsApp.Models.SuppliersStoreRow> SuppliersStore { get; set; } = null!;", 1)
  }

  Set-Content -Encoding UTF8 -Path $dbctx -Value $c
}

Write-Host "OK ✅ Fixed Program.cs + Models. Backup: $prog.before_pgonlyfix_$stamp.bak" -ForegroundColor Green
