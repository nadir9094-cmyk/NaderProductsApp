param()

$ErrorActionPreference = "Stop"
$prog = "C:\sami\Program.cs"

# Backup
Copy-Item $prog "$prog.bak_shiftsv2_$(Get-Date -Format yyyyMMdd_HHmmss)" -Force

$txt = Get-Content $prog -Raw -Encoding UTF8

if($txt -match "SHIFTS_API_V1"){
  Write-Host "OK: SHIFTS_API_V1 already exists."
  exit 0
}

$snippet = @"
//
// SHIFTS_API_V1
//
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NaderProductsApp.Data.AppDbContext>();

        db.Database.ExecuteSqlRaw(@`"
CREATE TABLE IF NOT EXISTS `"`"Shifts`"`"(
    `"`"Id`"`" serial PRIMARY KEY,
    `"`"CashierId`"`" integer NULL,
    `"`"CashierName`"`" text NULL,
    `"`"StartAt`"`" timestamp with time zone NOT NULL,
    `"`"EndAt`"`" timestamp with time zone NULL,
    `"`"OpeningCash`"`" numeric NULL,
    `"`"ClosingCash`"`" numeric NULL,
    `"`"Notes`"`" text NULL
);`");
        Console.WriteLine("DB PATCH OK: Shifts table ensured.");
    }
    catch { }
});

static (int? cashierId, string? cashierName) ReadCashierHeaders(HttpRequest req)
{
    var hId = req.Headers["x-cashier-id"].FirstOrDefault();
    var hName = req.Headers["x-cashier-name"].FirstOrDefault();
    int? cid = null;
    if (int.TryParse(hId, out var x)) cid = x;
    var cn = string.IsNullOrWhiteSpace(hName) ? null : hName!.Trim();
    return (cid, cn);
}

app.MapGet("/api/shifts/current", async (HttpRequest req, NaderProductsApp.Data.AppDbContext db) =>
{
    var (cid, cn) = ReadCashierHeaders(req);
    if (!cid.HasValue) return Results.Unauthorized();

    await using var conn = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

    await using var cmd = new Npgsql.NpgsqlCommand(@"
SELECT ""Id"", ""CashierId"", ""CashierName"", ""StartAt"", ""EndAt"", ""OpeningCash"", ""ClosingCash"", ""Notes""
FROM ""Shifts""
WHERE ""CashierId"" = @cid AND ""EndAt"" IS NULL
ORDER BY ""Id"" DESC
LIMIT 1;", conn);

    cmd.Parameters.AddWithValue("@cid", cid.Value);

    await using var r = await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync()) return Results.Ok(new { isOpen = false });

    return Results.Ok(new {
        isOpen = true,
        id = r.GetInt32(0),
        cashierId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1),
        cashierName = r.IsDBNull(2) ? null : r.GetString(2),
        startAt = r.GetDateTime(3),
        openingCash = r.IsDBNull(5) ? (decimal?)null : r.GetDecimal(5),
        notes = r.IsDBNull(7) ? null : r.GetString(7)
    });
});

app.MapPost("/api/shifts/open", async (HttpRequest req, NaderProductsApp.Data.AppDbContext db) =>
{
    var (cid, cn) = ReadCashierHeaders(req);
    if (!cid.HasValue) return Results.Unauthorized();

    var body = await req.ReadFromJsonAsync<System.Text.Json.JsonElement>();
    decimal? openingCash = null;
    string? notes = null;

    if (body.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
        if (body.TryGetProperty("openingCash", out var oc) && oc.ValueKind != System.Text.Json.JsonValueKind.Null)
            openingCash = oc.GetDecimal();
        if (body.TryGetProperty("notes", out var nt) && nt.ValueKind == System.Text.Json.JsonValueKind.String)
            notes = nt.GetString();
    }

    await using var conn = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

    // منع فتح ورديتين
    await using (var chk = new Npgsql.NpgsqlCommand(@"
SELECT 1 FROM ""Shifts""
WHERE ""CashierId""=@cid AND ""EndAt"" IS NULL
LIMIT 1;", conn))
    {
        chk.Parameters.AddWithValue("@cid", cid.Value);
        var exists = await chk.ExecuteScalarAsync();
        if (exists != null) return Results.BadRequest("SHIFT_ALREADY_OPEN");
    }

    await using (var ins = new Npgsql.NpgsqlCommand(@"
INSERT INTO ""Shifts"" (""CashierId"", ""CashierName"", ""StartAt"", ""OpeningCash"", ""Notes"")
VALUES (@cid, @cn, NOW(), @oc, @notes);", conn))
    {
        ins.Parameters.AddWithValue("@cid", cid.Value);
        ins.Parameters.AddWithValue("@cn", (object?)cn ?? DBNull.Value);
        ins.Parameters.AddWithValue("@oc", (object?)openingCash ?? DBNull.Value);
        ins.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
        await ins.ExecuteNonQueryAsync();
    }

    return Results.Ok(new { ok = true });
});

app.MapPost("/api/shifts/close", async (HttpRequest req, NaderProductsApp.Data.AppDbContext db) =>
{
    var (cid, cn) = ReadCashierHeaders(req);
    if (!cid.HasValue) return Results.Unauthorized();

    var body = await req.ReadFromJsonAsync<System.Text.Json.JsonElement>();
    decimal? closingCash = null;
    string? notes = null;

    if (body.ValueKind == System.Text.Json.JsonValueKind.Object)
    {
        if (body.TryGetProperty("closingCash", out var cc) && cc.ValueKind != System.Text.Json.JsonValueKind.Null)
            closingCash = cc.GetDecimal();
        if (body.TryGetProperty("notes", out var nt) && nt.ValueKind == System.Text.Json.JsonValueKind.String)
            notes = nt.GetString();
    }

    await using var conn = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

    await using var cmd = new Npgsql.NpgsqlCommand(@"
UPDATE ""Shifts""
SET ""EndAt"" = NOW(),
    ""ClosingCash"" = COALESCE(@cc, ""ClosingCash""),
    ""Notes"" = COALESCE(@notes, ""Notes"")
WHERE ""Id"" = (
    SELECT ""Id"" FROM ""Shifts""
    WHERE ""CashierId""=@cid AND ""EndAt"" IS NULL
    ORDER BY ""Id"" DESC
    LIMIT 1
);", conn);

    cmd.Parameters.AddWithValue("@cid", cid.Value);
    cmd.Parameters.AddWithValue("@cc", (object?)closingCash ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

    var n = await cmd.ExecuteNonQueryAsync();
    if (n == 0) return Results.BadRequest("NO_OPEN_SHIFT");
    return Results.Ok(new { ok = true });
});
//
// END_SHIFTS_API_V1
//
"@

# الحقن قبل app.Run("http://127.0.0.1:5050");
if($txt -match 'app\.Run\("http://127\.0\.0\.1:5050"\);\s*'){
  $txt = $txt -replace 'app\.Run\("http://127\.0\.0\.1:5050"\);\s*', ($snippet + "`r`napp.Run(""http://127.0.0.1:5050"");`r`n")
  Set-Content -Path $prog -Value $txt -Encoding UTF8
  Write-Host "OK: Shifts API inserted."
} else {
  throw "Could not find app.Run(""http://127.0.0.1:5050"");"
}

