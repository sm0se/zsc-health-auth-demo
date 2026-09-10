using Microsoft.AspNetCore.Authentication;

namespace Zsc.CommonRoutes;

/// <summary>
/// Configuration options for subscription key authentication.
/// Reads from Zsc:SubscriptionKeys in IConfiguration.
/// </summary>
public sealed class SubscriptionKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "SubscriptionKey";
    public const string HeaderName = "Ocp-Apim-Subscription-Key";

    /// <summary>
    /// The set of valid subscription keys, keyed by the key value itself.
    /// Provisioned from IConfiguration section Zsc:SubscriptionKeys.
    /// </summary>
    public IReadOnlySet<string> ValidKeys { get; set; } = new HashSet<string>(StringComparer.Ordinal);
}
