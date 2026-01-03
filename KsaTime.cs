// KsaTime.cs
// Place next to Program.cs (C:\sami\KsaTime.cs)
// Fix: provide static helpers usable from LINQ / expression trees.

using System;

public static class KsaTime
{
    private static readonly TimeSpan KsaOffset = TimeSpan.FromHours(3);

    public static string? ToKsaIso(DateTime? dt)
    {
        if (dt == null) return null;

        var v = dt.Value;
        var utc = v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc) // Unspecified => treat as UTC
        };

        var dto = new DateTimeOffset(utc).ToOffset(KsaOffset);
        return dto.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz");
    }

    public static string? IsoUtcTextToKsaIso(string? isoText)
    {
        if (string.IsNullOrWhiteSpace(isoText)) return null;
        var s = isoText.Trim();

        // If text has NO timezone, assume UTC text
        var hasTz = s.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || (s.Length >= 6 && (s[^6] == '+' || s[^6] == '-') && s[^3] == ':');

        if (!hasTz) s += "Z";

        if (!DateTimeOffset.TryParse(s, out var dto)) return isoText;
        var ksa = dto.ToUniversalTime().ToOffset(KsaOffset);
        return ksa.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz");
    }
}
