using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

// Outcome of checking a caller-supplied subscription key against the
// provisioned ones. Kept separate from Valid/Invalid so a missing header and a
// wrong key produce different AuthenticateResults (NoResult vs Fail) - see
// SubscriptionKeyAuthenticationHandler.
public enum SubscriptionKeyValidationResult
{
    Missing,
    Invalid,
    Valid,
}

// Checks the `Ocp-Apim-Subscription-Key` header value against the keys
// provisioned under `Zsc:SubscriptionKeys`. Deliberately separate from the
// authentication handler so the comparison itself is unit-testable without
// spinning up ASP.NET Core.
//
// Case-sensitive: a subscription key is an opaque provisioned secret, not a
// case-insensitive identifier, and the demo key deliberately mixes case
// (zsc-demo-subscription-key-001 is all lower here, but nothing about the
// comparison should assume that).
public sealed class SubscriptionKeyValidator
{
    public const string SectionName = "Zsc:SubscriptionKeys";

    private readonly IReadOnlySet<string> _keys;

    public SubscriptionKeyValidator(IConfiguration configuration)
    {
        _keys = configuration.GetSection(SectionName)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private SubscriptionKeyValidator(IEnumerable<string> keys)
    {
        _keys = keys.ToHashSet(StringComparer.Ordinal);
    }

    // Test-only construction path: unit tests exercise the comparison directly
    // without spinning up IConfiguration. Kept as a named factory rather than a
    // second public constructor so DI containers (which see only the
    // IConfiguration constructor) never have to choose between them.
    public static SubscriptionKeyValidator ForKeys(IEnumerable<string> keys) => new(keys);

    public SubscriptionKeyValidationResult Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return SubscriptionKeyValidationResult.Missing;
        }

        return _keys.Contains(headerValue)
            ? SubscriptionKeyValidationResult.Valid
            : SubscriptionKeyValidationResult.Invalid;
    }
}
