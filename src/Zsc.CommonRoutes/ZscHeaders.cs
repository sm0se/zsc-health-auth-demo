namespace Zsc.CommonRoutes;

// The headers the ZSC platform moves between services. Anything not named here
// is dropped at the first hop - see TokenForwardingHandler.
public static class ZscHeaders
{
    public const string Authorization = "Authorization";
    public const string CorrelationId = "X-Correlation-Id";
    public const string SubscriptionKey = "Ocp-Apim-Subscription-Key";
}
