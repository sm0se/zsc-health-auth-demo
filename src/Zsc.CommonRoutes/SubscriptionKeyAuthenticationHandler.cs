using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Zsc.CommonRoutes;

// The subscription-key half of platform authentication.
//
// Only reached for requests the ZscSmart policy scheme forwards here - a
// health-status route that carries an Ocp-Apim-Subscription-Key header (see
// ZscAuth). A valid key becomes an authenticated principal; an unknown key
// fails authentication so the caller sees 401; a missing (or empty) header
// yields NoResult and the challenge still ends as 401.
//
// The key decision itself lives in SubscriptionKeyValidator so it stays
// unit-testable without a server.
public sealed class SubscriptionKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SubscriptionKeyValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var headerValue)
            || StringValues.IsNullOrEmpty(headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        return Task.FromResult(validator.Validate(headerValue.ToString()) switch
        {
            SubscriptionKeyResult.Valid => AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(ZscAuth.SubscriptionKeyScheme)),
                ZscAuth.SubscriptionKeyScheme)),
            SubscriptionKeyResult.Invalid => AuthenticateResult.Fail("Invalid subscription key."),
            _ => AuthenticateResult.NoResult(),
        });
    }
}
