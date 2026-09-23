using Microsoft.AspNetCore.Http;

namespace Zsc.CommonRoutes;

public static class HealthStatusRoutePredicate
{
    public static bool IsHealthStatusRoute(PathString path) =>
        path.Value?.StartsWith("/api/v1/health/", StringComparison.OrdinalIgnoreCase) == true ||
        path.Value?.StartsWith("/internal/health/", StringComparison.OrdinalIgnoreCase) == true;
}
