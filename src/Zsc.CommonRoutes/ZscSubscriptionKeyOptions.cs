using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

// Subscription key configuration for services that need it (Health Status).
// Subscription keys are read from the `Zsc:SubscriptionKeys` configuration
// section and are validated per-request.
//
// Unlike OAuth2 tokens which must be validated on every request, subscription
// keys are simple fixed credentials checked against a static allowlist.
public sealed record ZscSubscriptionKeyOptions(IReadOnlySet<string> ValidKeys)
{
    public const string SectionName = "Zsc:SubscriptionKeys";

    public static ZscSubscriptionKeyOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var keys = section.Get<string[]>() ?? Array.Empty<string>();

        if (keys.Length == 0)
        {
            throw new InvalidOperationException(
                $"Configuration section '{SectionName}' must define at least one subscription key.");
        }

        return new ZscSubscriptionKeyOptions(new HashSet<string>(keys, StringComparer.Ordinal));
    }

    public bool IsValid(string key) => ValidKeys.Contains(key);
}
