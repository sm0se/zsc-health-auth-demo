using Microsoft.Extensions.Primitives;
using Zsc.CommonRoutes;

namespace Zsc.ApiGateway;

// Gives every inbound request a correlation id if the caller did not supply one,
// and echoes it back on the response. From here on the id travels with the
// request down the whole chain (see TokenForwardingHandler).
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(ZscHeaders.CorrelationId, out var incoming) || StringValues.IsNullOrEmpty(incoming))
        {
            context.Request.Headers[ZscHeaders.CorrelationId] = Guid.NewGuid().ToString("n");
        }

        context.Response.Headers[ZscHeaders.CorrelationId] = context.Request.Headers[ZscHeaders.CorrelationId];
        return next(context);
    }
}
