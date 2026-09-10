namespace Zsc.CommonRoutes;

// Identifies paths that are health-status endpoints.
//
// Health-status endpoints accept subscription key authentication in addition to
// OAuth2. All other endpoints require OAuth2 only.
public static class ZscHealthStatusRoutes
{
    // Health-status paths start with /api/v1/health/ (gateway, interceptor, bff)
    // or /internal/health/ (health-status service itself).
    public static bool IsHealthStatusRoute(string path)
    {
        return path.StartsWith("/api/v1/health/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/internal/health/", StringComparison.OrdinalIgnoreCase);
    }
}
