public static class ShiftsStateV1
{
    private static readonly object _lock = new();
    private static bool _open = false;
    private static int _id = 0;
    private static DateTime? _openedAtUtc = null;

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

    public static object Open()
    {
        lock (_lock)
        {
            if (_open)
                return new { ok = false, error = "SHIFT_ALREADY_OPEN", id = _id, openedAtUtc = _openedAtUtc };

            _open = true;
            _id = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % int.MaxValue);
            _openedAtUtc = DateTime.UtcNow;

            return new { ok = true, id = _id, openedAtUtc = _openedAtUtc };
        }
    }

    public static object Close()
    {
        lock (_lock)
        {
            if (!_open)
                return new { ok = false, error = "SHIFT_NOT_OPEN" };

            _open = false;
            var closedAtUtc = DateTime.UtcNow;
            var id = _id;
            _id = 0;
            _openedAtUtc = null;

            return new { ok = true, id = id, closedAtUtc = closedAtUtc };
        }
    }
}
