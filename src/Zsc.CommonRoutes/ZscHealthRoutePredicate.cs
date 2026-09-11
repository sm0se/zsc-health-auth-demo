using Microsoft.AspNetCore.Http;

namespace Zsc.CommonRoutes;

// Where the Health Status API's routes live, defined once so every service in
// the chain agrees on it. ZscAuth's policy scheme uses this to decide whether a
// subscription key is even eligible to authenticate a request: the coexistence
// this requirement adds (subscription key OR bearer) is scoped to these paths
// only, everything else in the platform stays OAuth2-only.
//
// The public path (`/api/v1/health/...`) is what the gateway, interceptor and
// bff see; the internal path (`/internal/health/...`) is what health-status
// itself serves once the bff has resolved the route. Both need to be
// recognised, because each of those four processes runs its own copy of
// AddZscPlatformAuth and decides for itself.
public static class ZscHealthRoutePredicate
{
    private const string PublicPrefix = "/api/v1/health/";
    private const string InternalPrefix = "/internal/health/";

    public static bool IsHealthStatusRoute(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase)
            || value.StartsWith(InternalPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
