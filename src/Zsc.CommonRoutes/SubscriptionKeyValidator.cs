using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

// Result of checking one Ocp-Apim-Subscription-Key header value.
public enum SubscriptionKeyValidation
{
    Missing,
    Invalid,
    Valid,
}

// The key check for subscription-key authentication, kept separate from the
// ASP.NET Core authentication handler so it can be unit tested without hosting
// a request.
//
// Valid keys come from configuration (Zsc:SubscriptionKeys) - never hard-coded -
// so a real deployment provisions and rotates them the same way it does today,
// just under a different section name.
public sealed class SubscriptionKeyValidator
{
    public const string SectionName = "Zsc:SubscriptionKeys";

    private readonly IReadOnlySet<string> _validKeys;

    public SubscriptionKeyValidator(IConfiguration configuration)
        : this(ReadKeys(configuration))
    {
    }

    public SubscriptionKeyValidator(IEnumerable<string> validKeys)
    {
        _validKeys = new HashSet<string>(validKeys, StringComparer.Ordinal);
    }

    // Case-sensitive: a subscription key is an opaque token, not a case-folded
    // identifier.
    public SubscriptionKeyValidation Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return SubscriptionKeyValidation.Missing;
        }

        return _validKeys.Contains(headerValue) ? SubscriptionKeyValidation.Valid : SubscriptionKeyValidation.Invalid;
    }

    private static IEnumerable<string> ReadKeys(IConfiguration configuration) =>
        configuration.GetSection(SectionName)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
}
