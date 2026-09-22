namespace Zsc.CommonRoutes;

// The outcome of checking an Ocp-Apim-Subscription-Key header.
public enum SubscriptionKeyResult
{
    Missing,
    Invalid,
    Valid,
}

// Decides whether a subscription key header value is one of the provisioned
// keys. The keys live in configuration (Zsc:SubscriptionKeys) and are never
// hard-coded here; ZscAuth builds one validator per service from that service's
// IConfiguration. Comparison is ordinal on purpose: a subscription key is an
// opaque token, so "ZSC-DEMO-SUBSCRIPTION-KEY-001" is not the same key as
// "zsc-demo-subscription-key-001".
public sealed class SubscriptionKeyValidator(IReadOnlyCollection<string> provisionedKeys)
{
    private readonly IReadOnlySet<string> _provisionedKeys =
        provisionedKeys as IReadOnlySet<string>
        ?? new HashSet<string>(provisionedKeys, StringComparer.Ordinal);

    public SubscriptionKeyResult Validate(string? headerValue) =>
        string.IsNullOrWhiteSpace(headerValue)
            ? SubscriptionKeyResult.Missing
            : _provisionedKeys.Contains(headerValue)
                ? SubscriptionKeyResult.Valid
                : SubscriptionKeyResult.Invalid;
}
