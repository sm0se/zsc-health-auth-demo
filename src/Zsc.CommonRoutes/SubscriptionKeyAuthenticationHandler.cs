using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

// Authentication handler for subscription key (Ocp-Apim-Subscription-Key header).
//
// The handler reads the subscription key from the request header and validates it
// against a configurable set of valid keys from IConfiguration. A valid key
// produces an AuthenticateResult.Success with a minimal ClaimsPrincipal; a missing
// header produces NoResult (so other schemes can be tried); a wrong key produces
// AuthenticateResult.Fail.
public sealed class SubscriptionKeyAuthenticationHandler : AuthenticationHandler<SubscriptionKeyOptions>
{
    public const string Scheme = "SubscriptionKey";

    public SubscriptionKeyAuthenticationHandler(
        IOptionsMonitor<SubscriptionKeyOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var headerValue))
        {
            // No header present - let other schemes try.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = headerValue.ToString();

        // Check if the provided key is in the configured set of valid keys.
        if (Options.ValidKeys.Contains(providedKey))
        {
            // Valid key - create a minimal authenticated principal.
            var identity = new System.Security.Claims.ClaimsIdentity(Scheme);
            identity.AddClaim(new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                "subscription-key-holder"));

            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme);

            Logger.LogInformation("Subscription key authentication succeeded.");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        // Invalid key.
        Logger.LogWarning("Subscription key authentication failed: invalid key.");
        return Task.FromResult(AuthenticateResult.Fail("Invalid subscription key."));
    }
}
