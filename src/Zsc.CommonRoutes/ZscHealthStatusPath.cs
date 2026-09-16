using Microsoft.AspNetCore.Http;

namespace Zsc.CommonRoutes;

// Which paths are the Health Status API, for the platform's authentication
// policy scheme (see ZscAuth). Defined once so the gateway, Interceptor, BFF and
// health-status service all agree on it: the public path prefix the first three
// see, and the internal path prefix health-status serves it under.
public static class ZscHealthStatusPath
{
    private const string PublicPrefix = "/api/v1/health/";
    private const string InternalPrefix = "/internal/health/";

    public static bool IsHealthStatusPath(PathString path) => IsHealthStatusPath(path.Value ?? string.Empty);

    public static bool IsHealthStatusPath(string path) =>
        path.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(InternalPrefix, StringComparison.OrdinalIgnoreCase);
}
