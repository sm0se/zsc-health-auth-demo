using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

// Subscription keys provisioned for the platform, read from the
// `Zsc:SubscriptionKeys` configuration section (a JSON array) by whichever
// service terminates the SubscriptionKey scheme - see ZscAuth. No key is
// hard-coded: the demo provisions exactly one, `zsc-demo-subscription-key-001`,
// in each service's appsettings.json.
public sealed record ZscSubscriptionKeyOptions(IReadOnlySet<string> ValidKeys)
{
    public const string SectionName = "Zsc:SubscriptionKeys";

    public static ZscSubscriptionKeyOptions FromConfiguration(IConfiguration configuration)
    {
        var keys = configuration.GetSection(SectionName)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);

        return new ZscSubscriptionKeyOptions(keys);
    }
}
