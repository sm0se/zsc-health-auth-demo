namespace Zsc.CommonRoutes;

// The headers the ZSC platform moves between services. Anything not named here
// is dropped at the first hop - see TokenForwardingHandler.
public static class ZscHeaders
{
    public const string Authorization = "Authorization";
    public const string CorrelationId = "X-Correlation-Id";

    // Requirement R1: the alternative credential the Health Status API accepts
    // in place of (or alongside) a bearer. See SubscriptionKeyAuthenticationOptions.
    public const string SubscriptionKey = SubscriptionKeyAuthenticationOptions.HeaderName;
}
