namespace Zsc.CommonRoutes;

// The headers the ZSC platform moves between services. Anything not named here
// is dropped at the first hop - see TokenForwardingHandler.
public static class ZscHeaders
{
    public const string Authorization = "Authorization";
    public const string CorrelationId = "X-Correlation-Id";

    // R1: the Azure API Management convention for a subscription key. Only
    // ever consulted on health-status routes - see ZscHealthStatusRoutePolicy.
    public const string SubscriptionKey = "Ocp-Apim-Subscription-Key";
}
