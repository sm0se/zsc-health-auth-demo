using Microsoft.AspNetCore.Http;

namespace Zsc.CommonRoutes;

// Carries the caller's context onto the next hop.
//
// Each service in the chain validates the caller's credential for itself, so
// it has to travel the whole way down; the correlation id travels with it so
// one inbound request can be followed across every service it touches.
//
// Only the headers named in ZscHeaders are copied. An inbound header this
// handler does not know about does not reach the next service. That now
// includes Ocp-Apim-Subscription-Key (Requirement R1) alongside Authorization -
// health-status routes may be reached by either credential, and both need to
// survive every hop for that to work.
public sealed class TokenForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var inbound = httpContextAccessor.HttpContext?.Request;
        if (inbound is not null)
        {
            Forward(inbound, request, ZscHeaders.Authorization);
            Forward(inbound, request, ZscHeaders.CorrelationId);
            Forward(inbound, request, ZscHeaders.SubscriptionKey);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static void Forward(HttpRequest inbound, HttpRequestMessage outbound, string headerName)
    {
        if (inbound.Headers.TryGetValue(headerName, out var value))
        {
            outbound.Headers.Remove(headerName);
            outbound.Headers.TryAddWithoutValidation(headerName, (IEnumerable<string?>)value);
        }
    }
}
