using System.Collections.Concurrent;

public static class ShiftsStateV1
{
    private static readonly object _lock = new();

    private static bool _open = false;
    private static int _id = 0;
    private static DateTime? _openedAtUtc = null;

    // سجل مبسط للشفتات (مؤقت بالذاكرة)
    private static readonly List<Dictionary<string, object?>> _history = new();

    public static object Current()
    {
        lock (_lock)
        {
            return new {
                open = _open,
                id = _id,
                openedAtUtc = _openedAtUtc
            };
        }
    }

    public static object Open(string cashierName)
    {
        lock (_lock)
        {
            if (_open)
                return new { ok = false, error = "SHIFT_ALREADY_OPEN", id = _id, openedAtUtc = _openedAtUtc };

            _open = true;
            _id = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);
            _openedAtUtc = DateTime.UtcNow;

            // أضف سجل
            _history.Insert(0, new Dictionary<string, object?> {
                ["id"] = _id,
                ["openedAtUtc"] = _openedAtUtc,
                ["closedAtUtc"] = null,
                ["cashierName"] = cashierName,
                ["status"] = "open"
            });

            return new { ok = true, id = _id, openedAtUtc = _openedAtUtc };
        }
    }

    public static object Close()
    {
        lock (_lock)
        {
            if (!_open)
                return new { ok = false, error = "SHIFT_NOT_OPEN" };

            var closedAtUtc = DateTime.UtcNow;
            var id = _id;

            // حدّث آخر شفت مفتوح
            var row = _history.FirstOrDefault(x => (x.TryGetValue("id", out var v) && v is int vi && vi == id));
            if (row != null)
            {
                row["closedAtUtc"] = closedAtUtc;
                row["status"] = "closed";
            }

            _open = false;
            _id = 0;
            _openedAtUtc = null;

            return new { ok = true, id = id, closedAtUtc = closedAtUtc };
        }
    }

    public static object List(int days = 60, int take = 200)
    {
        lock (_lock)
        {
            var since = DateTime.UtcNow.AddDays(-Math.Max(1, days));
            var items = _history
                .Where(x => x.TryGetValue("openedAtUtc", out var v) && v is DateTime dt && dt >= since)
                .Take(Math.Max(1, take))
                .Select(x => x)
                .ToList();

            return new {
                total = items.Count,
                items = items
            };
        }
    }
}
