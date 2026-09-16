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
// the same thing: the OAuth2 bearer scheme, the subscription-key scheme, a
// policy scheme ("ZscSmart") that picks between them per request, and an
// authorization FallbackPolicy that requires an authenticated user on every
// endpoint which does not explicitly opt out.
//
// R1 scopes subscription-key authentication to the Health Status API alone
// (see ZscHealthRoutePolicy) and keeps it additive there rather than a hard
// cutover: a caller on a health-status route may present either credential.
// Every other route is OAuth2-only, because ZscSmart only ever forwards to
// SubscriptionKey when both the header is present AND the path is a
// health-status route - everywhere else it forwards straight to Bearer, so a
// subscription key on e.g. /api/v1/devices/... is never even considered.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = SubscriptionKeyAuthenticationHandler.SchemeName;
    public const string SmartScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(options);
        services.AddSingleton(SubscriptionKeyValidator.FromConfiguration(configuration));

        services.AddAuthentication(SmartScheme)
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
            .AddScheme<AuthenticationSchemeOptions, SubscriptionKeyAuthenticationHandler>(SubscriptionKeyScheme, _ => { })
            .AddPolicyScheme(SmartScheme, SmartScheme, policy =>
            {
                policy.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(ZscHeaders.SubscriptionKey)
                    && ZscHealthRoutePolicy.IsHealthStatusRoute(context.Request.Path)
                        ? SubscriptionKeyScheme
                        : OAuth2Scheme;
            });

        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder(SmartScheme)
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
