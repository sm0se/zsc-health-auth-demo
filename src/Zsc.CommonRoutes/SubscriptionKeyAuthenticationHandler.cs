using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

// The "SubscriptionKey" scheme: authenticates a caller who presents a valid
// Ocp-Apim-Subscription-Key header. Registered alongside the OAuth2 bearer
// scheme by AddZscPlatformAuth; which one actually runs for a given request is
// decided by the "ZscSmart" policy scheme, not by this handler.
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
        var headerValue = Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var values)
            ? values.ToString()
            : null;

        return Task.FromResult(validator.Validate(headerValue) switch
        {
            SubscriptionKeyValidation.Valid => AuthenticateResult.Success(BuildTicket()),
            SubscriptionKeyValidation.Invalid => AuthenticateResult.Fail("The subscription key is not recognised."),
            _ => AuthenticateResult.NoResult(),
        });
    }

    private AuthenticationTicket BuildTicket()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.AuthenticationMethod, SchemeName) }, SchemeName);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
    }
}
