namespace Zsc.CommonRoutes;

// Defines, in one place, which paths are "the Health Status API" for the
// purpose of R1: those are the only routes where a subscription key is ever
// considered as an alternative to an OAuth2 bearer.
//
// The prefix differs by hop because the public path (gateway, interceptor,
// bff) and the downstream path (health-status's own routes) are different
// strings for the same product endpoint - see ZscRoutes for the mapping.
public static class ZscHealthStatusRoutePolicy
{
    public const string PublicPrefix = "/api/v1/health/";
    public const string InternalPrefix = "/internal/health/";

    public static bool IsHealthStatusRoute(string path) =>
        path.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(InternalPrefix, StringComparison.OrdinalIgnoreCase);
}
