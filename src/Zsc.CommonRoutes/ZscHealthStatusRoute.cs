namespace Zsc.CommonRoutes;

// Identifies which paths belong to the Health Status API - the one API in the
// platform that accepts a subscription key as an alternative to OAuth2 (see
// docs/REQUIREMENT-R1.md, "Coexistence" in docs/CHANGES-R1.md). Every other
// path stays OAuth2-only.
//
// Defined once here so the policy scheme in ZscAuth and every service that
// hosts a hop of this route (gateway, interceptor, bff, health-status) agree on
// exactly the same set of paths, instead of each re-deriving it.
public static class ZscHealthStatusRoute
{
    // The public-facing prefix, used by api-gateway, interceptor and bff.
    public const string PublicPrefix = "/api/v1/health/";

    // The prefix health-status itself serves the same routes under, once the
    // BFF has resolved them via ZscRoutes.
    public const string InternalPrefix = "/internal/health/";

    public static bool IsHealthStatusRoute(string path) =>
        !string.IsNullOrEmpty(path) &&
        (path.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase) ||
         path.StartsWith(InternalPrefix, StringComparison.OrdinalIgnoreCase));
}
