using Microsoft.AspNetCore.Routing;

public static class DiagRoutesApiV1
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/_diag/routes", (IEnumerable<EndpointDataSource> sources) =>
        {
            var routes = sources
                .SelectMany(s => s.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(e => e.RoutePattern.RawText)
                .Where(r => r != null && r.StartsWith("/api/"))
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            return Results.Ok(routes);
        });
    }
}
