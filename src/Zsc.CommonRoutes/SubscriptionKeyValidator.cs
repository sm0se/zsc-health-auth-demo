using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

// Outcome of checking a subscription-key header value against the provisioned
// keys. Kept separate from AuthenticateResult so it can be unit tested without
// standing up ASP.NET Core authentication at all.
public enum SubscriptionKeyOutcome
{
    Missing,
    Invalid,
    Valid,
}

// Checks an `Ocp-Apim-Subscription-Key` header value against the keys
// provisioned under `Zsc:SubscriptionKeys` in configuration. No key is
// hard-coded here; the demo provisions exactly one (see appsettings.json in
// each service), but the validator itself does not know or care how many
// there are.
//
// Comparison is exact (ordinal, case-sensitive): a subscription key is an
// opaque token, not a case-insensitive identifier like a header name.
public sealed class SubscriptionKeyValidator
{
    public const string SectionName = "Zsc:SubscriptionKeys";

    private readonly IReadOnlySet<string> _keys;

    public SubscriptionKeyValidator(IConfiguration configuration)
        : this(configuration.GetSection(SectionName).GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!))
    {
    }

    public SubscriptionKeyValidator(IEnumerable<string> provisionedKeys)
    {
        _keys = new HashSet<string>(provisionedKeys, StringComparer.Ordinal);
    }

    public SubscriptionKeyOutcome Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return SubscriptionKeyOutcome.Missing;
        }

        return _keys.Contains(headerValue) ? SubscriptionKeyOutcome.Valid : SubscriptionKeyOutcome.Invalid;
    }
}
