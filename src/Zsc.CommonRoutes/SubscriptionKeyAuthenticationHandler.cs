using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

public sealed class SubscriptionKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
}

// Terminates the 'SubscriptionKey' scheme: reads the Ocp-Apim-Subscription-Key
// header and asks SubscriptionKeyValidator whether it is one of the keys
// provisioned under Zsc:SubscriptionKeys. The scheme itself does not decide
// when it applies - the 'ZscSmart' policy scheme (see ZscAuth) only ever
// forwards to this one on a health-status route.
public sealed class SubscriptionKeyAuthenticationHandler(
    IOptionsMonitor<SubscriptionKeyAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SubscriptionKeyValidator validator)
    : AuthenticationHandler<SubscriptionKeyAuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SubscriptionKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var outcome = validator.Validate(headerValues.ToString());

        switch (outcome)
        {
            case SubscriptionKeyOutcome.Missing:
                // Header was present but empty - treat like absent.
                return Task.FromResult(AuthenticateResult.NoResult());

            case SubscriptionKeyOutcome.Invalid:
                return Task.FromResult(AuthenticateResult.Fail("The subscription key is not recognised."));

            case SubscriptionKeyOutcome.Valid:
                var identity = new ClaimsIdentity(SchemeName);
                identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, SchemeName));
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(ticket));

            default:
                throw new InvalidOperationException($"Unhandled {nameof(SubscriptionKeyOutcome)}: {outcome}.");
        }
    }
}
