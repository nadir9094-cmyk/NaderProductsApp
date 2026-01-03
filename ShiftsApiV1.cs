using Microsoft.AspNetCore.Mvc;

public static class ShiftsApiV1
{
    public static void MapShiftsApi(WebApplication app)
    {
        app.MapGet("/api/shifts/current", (HttpRequest http) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            return Results.Ok(ShiftsStateV1.Current());
        });
    }

    public static void MapShiftListApi(WebApplication app)
    {
        app.MapGet("/api/shifts/list", (HttpRequest http, int days = 60, int take = 200) =>
        {
            var token = EmployeesApiV1.ReadBearerToken(http);
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            // أولاً: جرّب history
            var listObj = ShiftsStateV1.List(days, take);

            // لو فاضي، رجّع الشفت الحالي لو مفتوح
            var curr = ShiftsStateV1.Current();
            var currOpenProp = curr.GetType().GetProperty("open");
            var currIdProp   = curr.GetType().GetProperty("id");
            var currAtProp   = curr.GetType().GetProperty("openedAtUtc");

            var isOpen = (currOpenProp?.GetValue(curr) as bool?) ?? false;
            var id     = (currIdProp?.GetValue(curr) as int?) ?? 0;
            var at     = currAtProp?.GetValue(curr);

            // استخراج total من listObj (بدون ما نعتمد على types معقدة)
            var totalProp = listObj.GetType().GetProperty("total");
            var total = (totalProp?.GetValue(listObj) as int?) ?? 0;

            if (total == 0 && isOpen && id != 0)
            {
                return Results.Ok(new
                {
                    total = 1,
                    items = new object[] {
                        new {
                            id = id,
                            openedAtUtc = at,
                            closedAtUtc = (DateTime?)null,
                            cashierName = "cashier",
                            status = "open"
                        }
                    }
                });
            }

            return Results.Ok(listObj);
        });
    }
}
