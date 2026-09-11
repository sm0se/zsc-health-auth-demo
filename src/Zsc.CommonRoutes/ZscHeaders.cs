namespace Zsc.CommonRoutes;

// The headers the ZSC platform moves between services. Anything not named here
// is dropped at the first hop - see TokenForwardingHandler.
public static class ZscHeaders
{
    public const string Authorization = "Authorization";
    public const string CorrelationId = "X-Correlation-Id";

    // Requirement R1: the Health Status API's subscription-key credential, in
    // the Azure API Management convention. Allowlisted at the edge
    // (EdgeHeaderPolicyMiddleware) and forwarded at every hop
    // (TokenForwardingHandler) exactly like Authorization.
    public const string SubscriptionKey = "Ocp-Apim-Subscription-Key";
}
