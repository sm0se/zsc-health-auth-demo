using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

// Terminates the "SubscriptionKey" scheme: reads Ocp-Apim-Subscription-Key and
// asks SubscriptionKeyValidator whether it is provisioned. A missing header is
// NoResult (so a policy scheme can fall through to another scheme instead of
// failing outright); an unrecognised key is a hard Fail, not a fall-through -
// a wrong key must not be treated as "no credentials".
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
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        return Task.FromResult(validator.Validate(headerValue.ToString()) switch
        {
            SubscriptionKeyValidationResult.Valid => AuthenticateResult.Success(BuildTicket()),
            SubscriptionKeyValidationResult.Invalid => AuthenticateResult.Fail("The subscription key is not recognised."),
            _ => AuthenticateResult.NoResult(),
        });
    }

    private AuthenticationTicket BuildTicket()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "subscription-key-client") },
            SchemeName);

        return new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
    }
}
