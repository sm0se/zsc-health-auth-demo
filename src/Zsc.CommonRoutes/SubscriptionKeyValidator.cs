using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

public enum SubscriptionKeyValidationResult
{
    Missing,
    Invalid,
    Valid,
}

// The key check on its own, with no dependency on ASP.NET Core authentication
// types, so it is testable without standing up a host. SubscriptionKeyAuthenticationHandler
// calls it; it never decides anything about the request pipeline itself.
public sealed class SubscriptionKeyValidator(IReadOnlyCollection<string> validKeys)
{
    public const string SectionName = "Zsc:SubscriptionKeys";

    public static SubscriptionKeyValidator FromConfiguration(IConfiguration configuration)
    {
        var keys = configuration.GetSection(SectionName)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        return new SubscriptionKeyValidator(keys);
    }

    public SubscriptionKeyValidationResult Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return SubscriptionKeyValidationResult.Missing;
        }

        return validKeys.Contains(headerValue, StringComparer.Ordinal)
            ? SubscriptionKeyValidationResult.Valid
            : SubscriptionKeyValidationResult.Invalid;
    }
}
