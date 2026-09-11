using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Zsc.CommonRoutes;

// Platform-wide authentication.
//
// Every ZSC service calls AddZscPlatformAuth in its Program.cs and gets the
// same three schemes:
//
//   - 'Bearer'         - the OAuth2 bearer scheme. Still the only way into
//                         every ZSC API other than the Health Status API.
//   - 'SubscriptionKey' - R1's alternative for the Health Status API. See
//                         SubscriptionKeyAuthenticationHandler.
//   - 'ZscSmart'        - a policy scheme (AddPolicyScheme) that picks between
//                         the two above per request, without any service
//                         declaring per-route authorization metadata: it
//                         forwards to 'SubscriptionKey' when the request
//                         carries the subscription-key header AND the path is
//                         a health-status route (ZscHealthStatusRoutePolicy),
//                         and to 'Bearer' otherwise.
//
// 'ZscSmart' is the default authenticate/challenge scheme, and the
// authorization FallbackPolicy still just requires an authenticated user - so
// this stays a process-wide decision at startup, exactly as before R1. The
// per-route difference lives entirely in the scheme selector, not in
// authorization metadata scattered across endpoints.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = SubscriptionKeyAuthenticationHandler.SchemeName;
    public const string SmartScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(options);
        services.AddSingleton(new SubscriptionKeyValidator(configuration));

        services.AddAuthentication(SmartScheme)
            .AddPolicyScheme(SmartScheme, SmartScheme, policy =>
            {
                policy.ForwardDefaultSelector = SelectScheme;
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
            .AddScheme<SubscriptionKeyAuthenticationSchemeOptions, SubscriptionKeyAuthenticationHandler>(
                SubscriptionKeyScheme, _ => { });

        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder(SmartScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    // The coexistence deviation from a hard cutover: on a health-status route,
    // a caller who sent the subscription-key header is authenticated against
    // it; everyone else - and every non-health-status route, regardless of
    // what headers it carries - goes through the OAuth2 bearer scheme, exactly
    // as before R1.
    private static string SelectScheme(HttpContext context)
    {
        var isHealthStatusRoute = ZscHealthStatusRoutePolicy.IsHealthStatusRoute(context.Request.Path.Value ?? string.Empty);
        var hasSubscriptionKey = context.Request.Headers.ContainsKey(ZscHeaders.SubscriptionKey);

        return isHealthStatusRoute && hasSubscriptionKey ? SubscriptionKeyScheme : OAuth2Scheme;
    }

    public static WebApplication UseZscPlatformAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
