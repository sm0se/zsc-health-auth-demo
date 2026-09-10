using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

/// <summary>
/// Authenticates requests that carry a valid Ocp-Apim-Subscription-Key header.
/// </summary>
public sealed class SubscriptionKeyAuthenticationHandler : AuthenticationHandler<SubscriptionKeyAuthenticationOptions>
{
    public SubscriptionKeyAuthenticationHandler(
        IOptionsMonitor<SubscriptionKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Create a validator from the options
        var validator = new SubscriptionKeyValidator(Options);

        // Check if the request carries the subscription key header
        if (!Request.Headers.TryGetValue(SubscriptionKeyAuthenticationOptions.HeaderName, out var headerValue))
        {
            // No header - return NoResult so the policy scheme can try another scheme
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var key = headerValue.ToString();

        // Validate the key
        var result = validator.Validate(key);
        if (result == SubscriptionKeyValidator.ValidationResult.Invalid)
        {
            // Invalid key - authentication failed
            return Task.FromResult(AuthenticateResult.Fail($"Invalid subscription key: {SubscriptionKeyAuthenticationOptions.HeaderName}"));
        }

        if (result == SubscriptionKeyValidator.ValidationResult.Missing)
        {
            // Empty header - authentication failed
            return Task.FromResult(AuthenticateResult.Fail($"Empty subscription key"));
        }

        // Valid key - create a principal with a single claim identifying the key
        // No scopes or per-consumer data; authentication only
        var claims = new[] { new Claim(ClaimTypes.Name, key) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
