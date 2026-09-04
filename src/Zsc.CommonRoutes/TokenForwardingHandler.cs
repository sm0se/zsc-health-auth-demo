using Microsoft.AspNetCore.Http;

namespace Zsc.CommonRoutes;

// Carries the caller's context onto the next hop.
//
// Each service in the chain validates the caller's OAuth2 bearer for itself, so
// the token has to travel the whole way down; the correlation id travels with it
// so one inbound request can be followed across every service it touches.
// Similarly, subscription keys (used for Health Status endpoints) are forwarded.
//
// Only the headers named in ZscHeaders are copied. An inbound header this
// handler does not know about does not reach the next service.
public sealed class TokenForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var inbound = httpContextAccessor.HttpContext?.Request;
        if (inbound is not null)
        {
            if (inbound.Headers.TryGetValue(ZscHeaders.Authorization, out var authorization))
            {
                request.Headers.Remove(ZscHeaders.Authorization);
                request.Headers.TryAddWithoutValidation(ZscHeaders.Authorization, (IEnumerable<string?>)authorization);
            }

            if (inbound.Headers.TryGetValue(ZscHeaders.CorrelationId, out var correlationId))
            {
                request.Headers.Remove(ZscHeaders.CorrelationId);
                request.Headers.TryAddWithoutValidation(ZscHeaders.CorrelationId, (IEnumerable<string?>)correlationId);
            }

            if (inbound.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var subscriptionKey))
            {
                request.Headers.Remove(ZscHeaders.SubscriptionKey);
                request.Headers.TryAddWithoutValidation(ZscHeaders.SubscriptionKey, (IEnumerable<string?>)subscriptionKey);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
