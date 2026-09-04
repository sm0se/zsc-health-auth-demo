using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

// Subscription key authentication scheme for the Health Status API.
//
// Unlike OAuth2 which validates a bearer token with cryptographic signatures,
// subscription key authentication is a simple header check: the key must be
// present, non-empty, and registered in configuration.
//
// This scheme is separate from OAuth2 and is installed alongside it. Services
// that use it declare per-route which endpoints use which scheme.
public static class ZscSubscriptionKeyAuth
{
    public const string SchemeName = "SubscriptionKey";

    public static IServiceCollection AddZscSubscriptionKeyAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ZscSubscriptionKeyOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, SubscriptionKeyAuthenticationHandler>(SchemeName, _ => { });

        return services;
    }
}

// Handler that implements subscription key validation. It expects the
// Ocp-Apim-Subscription-Key header and validates it against the configured set.
file sealed class SubscriptionKeyAuthenticationHandler(
    ZscSubscriptionKeyOptions options,
    IOptionsMonitor<AuthenticationSchemeOptions> monitor,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(monitor, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ZscHeaders.SubscriptionKey, out var keyValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var key = keyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!options.IsValid(key))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Invalid {ZscHeaders.SubscriptionKey}."));
        }

        // Create a minimal principal with the subscription key as identity.
        // Subscription keys authenticate but do not authorize (no claims/scopes).
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, key) },
                Scheme.Name));

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
