param()

$root = "C:\sami"
$prog = Join-Path $root "Program.cs"
$dbctx = Join-Path $root "Data\AppDbContext.cs"
$modelsDir = Join-Path $root "Models"
$endpointsFile = Join-Path $root "PgOnlyEndpoints.cs"

if(!(Test-Path $prog)){ throw "Program.cs not found: $prog" }

# جرّب الباك أبس إلى أن نجد نسخة تبني
$backups = Get-ChildItem $root -Filter "Program.cs*.bak" -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
if(!$backups){ throw "No Program.cs backups found (*.bak) in $root" }

$stamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
$workbak = Join-Path $root ("Program.cs.WORKING_{0}.bak" -f $stamp)

$found = $false
foreach($b in $backups){
  Copy-Item $b.FullName $prog -Force
  dotnet build | Out-Null
  if($LASTEXITCODE -eq 0){
    Copy-Item $prog $workbak -Force
    Write-Host ("✅ Found working backup: {0}" -f $b.Name) -ForegroundColor Green
    $found = $true
    break
  }
}
if(-not $found){ throw "Could not find ANY backup that builds." }

# Models folder
if(!(Test-Path $modelsDir)){ New-Item -ItemType Directory -Path $modelsDir | Out-Null }

# Models files
@"
namespace Models;

public class AppSetting
{
    public int Id { get; set; } = 1;

    public string? StoreName { get; set; }
    public string? CommercialRegister { get; set; }
    public string? VatNumber { get; set; }
    public string? StoreAddress { get; set; }
    public string? StorePhone { get; set; }

    public string? InvoicePaper { get; set; }
    public int? CashierPaperWidthMm { get; set; }

    public string? StoreLogoBase64 { get; set; }
    public string? InvoiceFooterNotes { get; set; }

    public string? BarcodeType { get; set; }
    public int? ZatcaPhase { get; set; }

    public string? Phase2InvoiceHash { get; set; }
    public string? Phase2Signature { get; set; }
    public string? Phase2PublicKey { get; set; }
    public string? Phase2CertificateSignature { get; set; }
}
"@ | Set-Content -Encoding UTF8 -Path (Join-Path $modelsDir "AppSetting.cs")

@"
namespace Models;

public class SuppliersStoreRow
{
    public int Id { get; set; } = 1;
    public string? Json { get; set; }
}
"@ | Set-Content -Encoding UTF8 -Path (Join-Path $modelsDir "SuppliersStoreRow.cs")

@"
namespace Models;

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
"@ | Set-Content -Encoding UTF8 -Path (Join-Path $modelsDir "SettingsDto.cs")

# Endpoints file
@"
using Microsoft.EntityFrameworkCore;
using Models;

public static class PgOnlyEndpoints
{
    private const string DefaultSuppliersJson = @""{""""suppliers"""":[],""""invoices"""":[],""""payments"""":[]}""";

    private static async Task<AppSetting> EnsureSettingsRow(Data.AppDbContext db)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (row is null)
        {
            row = new AppSetting { Id = 1, InvoicePaper = ""Cashier"", CashierPaperWidthMm = 80, BarcodeType = ""QR"", ZatcaPhase = 1 };
            db.AppSettings.Add(row);
            await db.SaveChangesAsync();
        }
        return row;
    }

    private static async Task<SuppliersStoreRow> EnsureSuppliersStoreRow(Data.AppDbContext db)
    {
        var row = await db.SuppliersStore.FirstOrDefaultAsync(x => x.Id == 1);
        if (row is null)
        {
            row = new SuppliersStoreRow { Id = 1, Json = DefaultSuppliersJson };
            db.SuppliersStore.Add(row);
            await db.SaveChangesAsync();
        }
        if (string.IsNullOrWhiteSpace(row.Json)) row.Json = DefaultSuppliersJson;
        return row;
    }

    public static void MapPgOnly(WebApplication app)
    {
        app.MapGet(""/api/settings"", async (Data.AppDbContext db) =>
        {
            var row = await EnsureSettingsRow(db);
            return Results.Ok(new
            {
                storeName = row.StoreName ?? """",
                commercialRegister = row.CommercialRegister ?? """",
                vatNumber = row.VatNumber ?? """",
                storeAddress = row.StoreAddress ?? """",
                storePhone = row.StorePhone ?? """",
                invoicePaper = string.IsNullOrWhiteSpace(row.InvoicePaper) ? ""Cashier"" : row.InvoicePaper,
                cashierPaperWidthMm = row.CashierPaperWidthMm ?? 80,
                storeLogoBase64 = row.StoreLogoBase64 ?? """",
                invoiceFooterNotes = row.InvoiceFooterNotes ?? """",
                barcodeType = string.IsNullOrWhiteSpace(row.BarcodeType) ? ""QR"" : row.BarcodeType,
                zatcaPhase = row.ZatcaPhase ?? 1,
                phase2InvoiceHash = row.Phase2InvoiceHash ?? """",
                phase2Signature = row.Phase2Signature ?? """",
                phase2PublicKey = row.Phase2PublicKey ?? """",
                phase2CertificateSignature = row.Phase2CertificateSignature ?? """"
            });
        });

        app.MapPut(""/api/settings"", async (Data.AppDbContext db, HttpRequest request) =>
        {
            var dto = await request.ReadFromJsonAsync<SettingsDto>();
            if (dto is null) return Results.BadRequest(""INVALID_BODY"");

            var row = await EnsureSettingsRow(db);

            var invoicePaper = (dto.InvoicePaper ?? ""Cashier"").Trim();
            if (invoicePaper != ""A4"" && invoicePaper != ""Cashier"") invoicePaper = ""Cashier"";

            var barcodeType = (dto.BarcodeType ?? ""QR"").Trim().ToUpperInvariant();
            if (barcodeType != ""QR"") barcodeType = ""QR"";

            var phase = dto.ZatcaPhase == 2 ? 2 : 1;
            var width = dto.CashierPaperWidthMm <= 0 ? 80 : dto.CashierPaperWidthMm;
            if (width < 58) width = 58;
            if (width > 120) width = 120;

            var logo = (dto.StoreLogoBase64 ?? """").Trim();
            if (logo.Length > 2000000) return Results.BadRequest(""LOGO_TOO_LARGE"");

            row.StoreName = (dto.StoreName ?? """").Trim();
            row.CommercialRegister = (dto.CommercialRegister ?? """").Trim();
            row.VatNumber = (dto.VatNumber ?? """").Trim();
            row.StoreAddress = (dto.StoreAddress ?? """").Trim();
            row.StorePhone = (dto.StorePhone ?? """").Trim();

            row.InvoicePaper = invoicePaper;
            row.CashierPaperWidthMm = width;
            row.StoreLogoBase64 = logo;
            row.InvoiceFooterNotes = (dto.InvoiceFooterNotes ?? """").Trim();

            row.BarcodeType = barcodeType;
            row.ZatcaPhase = phase;

            row.Phase2InvoiceHash = phase == 2 ? (dto.Phase2InvoiceHash ?? """").Trim() : """";
            row.Phase2Signature = phase == 2 ? (dto.Phase2Signature ?? """").Trim() : """";
            row.Phase2PublicKey = phase == 2 ? (dto.Phase2PublicKey ?? """").Trim() : """";
            row.Phase2CertificateSignature = phase == 2 ? (dto.Phase2CertificateSignature ?? """").Trim() : """";

            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        app.MapGet(""/api/suppliers-store"", async (Data.AppDbContext db) =>
        {
            var row = await EnsureSuppliersStoreRow(db);
            return Results.Text(row.Json ?? DefaultSuppliersJson, ""application/json; charset=utf-8"");
        });

        app.MapPut(""/api/suppliers-store"", async (Data.AppDbContext db, HttpRequest request) =>
        {
            using var sr = new StreamReader(request.Body);
            var raw = await sr.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(raw)) return Results.BadRequest(""EMPTY_BODY"");

            var row = await EnsureSuppliersStoreRow(db);
            row.Json = raw;
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });
    }
}
"@ | Set-Content -Encoding UTF8 -Path $endpointsFile

# Patch Program.cs safely (no null vars)
$p = Get-Content $prog -Raw -Encoding UTF8

$p = [regex]::Replace($p, '(?is)app\.Map(?:Get|Put|Post|Delete|Patch)\(\s*"/api/settings"\s*,.*?\)\s*;\s*', '')
$p = [regex]::Replace($p, '(?is)app\.Map(?:Get|Put|Post|Delete|Patch)\(\s*"/api/suppliers-store"\s*,.*?\)\s*;\s*', '')

$p = [regex]::Replace($p, '(?s)//\s*=======================\s*SUPPLIERS STORE \(PERSISTED\)\s*=======================.*?//\s*====================\s*END SUPPLIERS STORE\s*====================\s*', '')
$p = [regex]::Replace($p, '(?s)//\s*=======================\s*SETTINGS \(PERSISTED\)\s*=======================.*?//\s*====================\s*END SETTINGS\s*====================\s*', '')

if($p -notmatch [regex]::Escape("PgOnlyEndpoints.MapPgOnly(app);")){
  if($p -match '(?m)^\s*app\.Run\s*\('){
    $p = [regex]::Replace($p, '(?m)^\s*app\.Run\s*\(', "PgOnlyEndpoints.MapPgOnly(app);`r`n`r`napp.Run(", 1)
  } else {
    $p += "`r`nPgOnlyEndpoints.MapPgOnly(app);`r`n"
  }
}

Set-Content -Encoding UTF8 -Path $prog -Value $p

# Patch DbContext if exists
if(Test-Path $dbctx){
  $c = Get-Content $dbctx -Raw -Encoding UTF8
  if($c -notmatch 'DbSet\s*<\s*Models\.AppSetting\s*>\s*AppSettings'){
    $c = [regex]::Replace($c, '(?m)^\s*public\s+class\s+AppDbContext\s*:\s*DbContext\s*\{',
      '$0' + "`r`n    public DbSet<Models.AppSetting> AppSettings { get; set; } = null!;", 1)
  }
  if($c -notmatch 'DbSet\s*<\s*Models\.SuppliersStoreRow\s*>\s*SuppliersStore'){
    $c = [regex]::Replace($c, '(?m)^\s*public\s+class\s+AppDbContext\s*:\s*DbContext\s*\{',
      '$0' + "`r`n    public DbSet<Models.SuppliersStoreRow> SuppliersStore { get; set; } = null!;", 1)
  }
  Set-Content -Encoding UTF8 -Path $dbctx -Value $c
}

Write-Host "✅ Done. WORKING Program.cs backup: $workbak" -ForegroundColor Green
