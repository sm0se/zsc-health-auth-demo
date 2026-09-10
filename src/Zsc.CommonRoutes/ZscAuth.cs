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
// Every ZSC service calls AddZscPlatformAuth in its Program.cs. This registers:
//
// 1. JwtBearer scheme ('Bearer'): OAuth2 bearer token validation.
// 2. SubscriptionKeyAuthenticationHandler ('SubscriptionKey'): Ocp-Apim-Subscription-Key validation.
// 3. A policy scheme ('ZscSmart') that chooses between them:
//    - If the request carries Ocp-Apim-Subscription-Key AND the path is a health-status route,
//      use SubscriptionKey scheme.
//    - Otherwise, use Bearer scheme.
//
// 'ZscSmart' is the DefaultAuthenticateScheme and DefaultChallengeScheme, so every request
// goes through this logic. A FallbackPolicy requires authentication on every endpoint that
// does not explicitly opt out - see ZscLiveness for the anonymous /healthz endpoints.
//
// Requirement R1 deviation: Health Status API accepts EITHER a valid subscription key OR
// a valid OAuth2 bearer. Non-health-status endpoints accept bearer only.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = SubscriptionKeyAuthenticationOptions.Scheme;
    public const string SmartPolicyScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var oauth2Options = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(oauth2Options);

        // Load subscription keys from configuration.
        var subscriptionKeys = configuration.GetSection("Zsc:SubscriptionKeys").Get<Dictionary<string, bool>>()
                                 ?? new Dictionary<string, bool>();
        var validKeys = new HashSet<string>(subscriptionKeys.Keys, StringComparer.Ordinal);

        services.AddAuthentication(SmartPolicyScheme)
            // OAuth2 bearer scheme.
            .AddJwtBearer(OAuth2Scheme, jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = oauth2Options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = oauth2Options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(oauth2Options.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            })
            // Subscription key scheme - register with the valid keys.
            .AddScheme<SubscriptionKeyAuthenticationOptions, SubscriptionKeyAuthenticationHandler>(
                SubscriptionKeyScheme, opts => opts.ValidKeys = validKeys)
            // Policy scheme that chooses between the above two based on the request context.
            .AddPolicyScheme(SmartPolicyScheme, "Smart policy (Bearer or SubscriptionKey)", opts =>
            {
                opts.ForwardDefaultSelector = context =>
                {
                    // If the request carries the subscription key header AND the path is a health-status route,
                    // use the SubscriptionKey scheme. Otherwise, use Bearer.
                    if (context.Request.Headers.ContainsKey(SubscriptionKeyAuthenticationOptions.HeaderName) &&
                        ZscRoutes.IsHealthStatusRoute(context.Request.Path.Value ?? ""))
                    {
                        return SubscriptionKeyScheme;
                    }

                    return OAuth2Scheme;
                };
            });

        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder(SmartPolicyScheme)
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
