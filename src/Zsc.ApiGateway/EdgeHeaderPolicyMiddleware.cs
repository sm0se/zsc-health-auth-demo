using Zsc.CommonRoutes;

namespace Zsc.ApiGateway;

// Edge header hygiene.
//
// The gateway is the only service exposed to callers outside the platform, so it
// is where untrusted input is narrowed: an inbound request keeps the headers
// this policy names and loses every other one before it enters the internal
// chain. A header the platform has no use for never reaches the Interceptor.
public sealed class EdgeHeaderPolicyMiddleware(RequestDelegate next)
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ZscHeaders.Authorization,
        ZscHeaders.CorrelationId,
        ZscHeaders.SubscriptionKey,
        "Accept",
        "Accept-Encoding",
        "Content-Type",
        "Content-Length",
        "Host",
        "User-Agent",
    };

    public Task InvokeAsync(HttpContext context)
    {
        foreach (var name in context.Request.Headers.Keys.Where(key => !Allowed.Contains(key)).ToList())
        {
            context.Request.Headers.Remove(name);
        }

        return next(context);
    }
}
