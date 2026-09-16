namespace Zsc.CommonRoutes;

// Which routes are the Health Status API, defined once so every service and
// every test that cares about the boundary agree on the same answer. R1 scopes
// subscription-key authentication to exactly these paths - everything else on
// the platform stays OAuth2-only.
public static class ZscHealthRoutePolicy
{
    private static readonly string[] Prefixes = { "/api/v1/health/", "/internal/health/" };

    public static bool IsHealthStatusRoute(string path) =>
        Prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
