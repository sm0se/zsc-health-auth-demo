using Microsoft.AspNetCore.Authentication;

namespace Zsc.CommonRoutes;

// Options for the SubscriptionKey authentication scheme.
//
// ValidKeys is populated from IConfiguration under Zsc:SubscriptionKeys.
public sealed class SubscriptionKeyOptions : AuthenticationSchemeOptions
{
    public IReadOnlySet<string> ValidKeys { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
