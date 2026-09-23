using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

public sealed class SubscriptionKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SubscriptionKeyValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var result = validator.Validate(headerValue.ToString());
        if (result == SubscriptionKeyValidationResult.Missing)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (result == SubscriptionKeyValidationResult.Invalid)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid subscription key."));
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "subscription-key") }, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
