using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Zsc.CommonRoutes;

// Platform-wide authentication.
//
// Every ZSC service calls AddZscPlatformAuth in its Program.cs and gets exactly
// the same thing: the OAuth2 bearer scheme, a subscription-key scheme for the
// Health Status API, and a policy scheme ("ZscSmart") that picks between them
// per request, plus an authorization FallbackPolicy that requires an
// authenticated user on every endpoint which does not explicitly opt out.
// Authentication is a process-wide decision made once at startup - there is no
// per-route policy anywhere in the platform. The policy scheme forwards to the
// subscription-key scheme only on health-status routes that carry an
// Ocp-Apim-Subscription-Key header; every other request keeps being judged as
// OAuth2 (Requirement R1, with the coexistence deviation recorded in
// docs/CHANGES-R1.md).
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;

    public const string SubscriptionKeyScheme = "SubscriptionKey";

    public const string SmartScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(options);

        // Provisioned subscription keys come from configuration only; a service
        // with no Zsc:SubscriptionKeys section accepts no key at all.
        var subscriptionKeys = configuration.GetSection("Zsc:SubscriptionKeys").GetChildren()
            .Select(entry => entry.Key)
            .Where(key => !string.IsNullOrEmpty(key))
            .ToHashSet(StringComparer.Ordinal);
        services.AddSingleton(new SubscriptionKeyValidator(subscriptionKeys));

        services.AddAuthentication(authentication =>
        {
            // The smart scheme decides per request which credential to honour;
            // every authentication and challenge goes through it.
            authentication.DefaultAuthenticateScheme = SmartScheme;
            authentication.DefaultChallengeScheme = SmartScheme;
        })
            .AddJwtBearer(OAuth2Scheme, jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            })
            .AddScheme<AuthenticationSchemeOptions, SubscriptionKeyAuthenticationHandler>(
                SubscriptionKeyScheme, _ => { })
            .AddPolicyScheme(SmartScheme, SmartScheme, smart =>
            {
                smart.ForwardDefaultSelector = context =>
                    ZscRoutes.IsHealthStatusRoute(context.Request.Path)
                        && context.Request.Headers.ContainsKey(ZscHeaders.SubscriptionKey)
                        ? SubscriptionKeyScheme
                        : OAuth2Scheme;
            });

        services.AddAuthorization(authorization =>
        {
            // Deliberately scheme-agnostic: ZscSmart has already picked the
            // credential to honour by the time this policy runs, and both the
            // bearer and the subscription key count as an authenticated user.
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    public static WebApplication UseZscPlatformAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
