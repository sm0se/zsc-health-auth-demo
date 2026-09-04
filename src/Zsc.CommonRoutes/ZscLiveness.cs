using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Zsc.CommonRoutes;

public static class ZscLiveness
{
    // Process liveness, not a product API.
    //
    // `/healthz` answers "this process is up" for orchestrators and local
    // tooling. It is anonymous on every service and always has been. It is NOT
    // the ZSC/ZLS Health Status API - that is a product endpoint served by
    // Zsc.HealthStatus under /api/v1/health/{platform}/status, reached through
    // the full chain, and it is authenticated.
    public static WebApplication MapZscLiveness(this WebApplication app, string serviceName)
    {
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = serviceName }))
            .AllowAnonymous();

        return app;
    }
}
