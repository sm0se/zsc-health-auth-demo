using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

// The 'SubscriptionKey' authentication scheme (Requirement R1): validates the
// Ocp-Apim-Subscription-Key header via SubscriptionKeyValidator. Registered
// alongside the OAuth2 bearer scheme in ZscAuth.AddZscPlatformAuth; the
// 'ZscSmart' policy scheme decides per request which of the two actually runs.
//
// A missing header is NoResult, not Fail - it means "this scheme has nothing to
// say about this request", which lets the platform's other scheme (or the
// authorization fallback policy) decide instead. A present-but-wrong key is
// Fail, which is a hard rejection.
public sealed class SubscriptionKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SubscriptionKeyValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SubscriptionKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var result = validator.Validate(values.ToString());
        return Task.FromResult(result switch
        {
            SubscriptionKeyValidationResult.Valid => AuthenticateResult.Success(BuildTicket()),
            SubscriptionKeyValidationResult.Invalid => AuthenticateResult.Fail("Invalid subscription key."),
            _ => AuthenticateResult.NoResult(),
        });
    }

    private AuthenticationTicket BuildTicket()
    {
        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, SchemeName));
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, SchemeName);
    }
}
