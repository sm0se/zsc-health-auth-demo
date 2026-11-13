using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

// Authenticates a caller by the Ocp-Apim-Subscription-Key header against the
// keys in `Zsc:SubscriptionKeys`. Authentication only: a valid key yields an
// identity with no roles or scopes.
//
// The comparison is constant-time per candidate so a caller cannot learn a key
// prefix from response timing.
public sealed class SubscriptionKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ZscSubscriptionKeys keys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var supplied))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var candidate = Encoding.UTF8.GetBytes(supplied.ToString());
        foreach (var known in keys.Values)
        {
            var expected = Encoding.UTF8.GetBytes(known);
            if (candidate.Length == expected.Length && CryptographicOperations.FixedTimeEquals(candidate, expected))
            {
                var identity = new ClaimsIdentity(Scheme.Name);
                identity.AddClaim(new Claim(ClaimTypes.Name, "subscription-key"));
                var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        return Task.FromResult(AuthenticateResult.Fail("Unknown subscription key."));
    }
}

// The configured subscription keys, read once from `Zsc:SubscriptionKeys`.
public sealed record ZscSubscriptionKeys(IReadOnlyList<string> Values)
{
    public const string SectionName = "Zsc:SubscriptionKeys";

    public static ZscSubscriptionKeys FromConfiguration(IConfiguration configuration)
    {
        var values = configuration.GetSection(SectionName).Get<string[]>() ?? [];
        return new ZscSubscriptionKeys(values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray());
    }
}
