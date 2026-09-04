using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Zsc.CommonRoutes;

// Platform-wide authentication.
//
// OAuth2 bearer scheme is installed on every service. Services that use only
// OAuth2 (Interceptor, BFF, DeviceApi) use AddZscPlatformAuthWithFallback to
// add an authorization FallbackPolicy requiring OAuth2 on all endpoints.
//
// Services that support multiple schemes (HealthStatus) use AddZscPlatformAuthWithPerRoutePolicy
// to opt-out of the FallbackPolicy and instead declare policies on specific endpoints.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(options);

        services.AddAuthentication(OAuth2Scheme)
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
            });

        return services;
    }

    public static IServiceCollection AddZscPlatformAuthWithFallback(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddZscPlatformAuth(configuration);

        services.AddAuthorization(authorization =>
        {
            // Fallback policy: all endpoints require OAuth2 authentication unless they opt out.
            // Services that declare per-route policies do not use this fallback.
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder(OAuth2Scheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    public static IServiceCollection AddZscPlatformAuthWithPerRoutePolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddZscPlatformAuth(configuration);
        services.AddZscSubscriptionKeyAuth(configuration);

        services.AddAuthorization(authorization =>
        {
            // When using per-route policies, we need a default policy that denies
            // (rather than FallbackPolicy). This way, only endpoints with explicit
            // [Authorize] attributes will be accessible. Unauthenticated endpoints
            // must opt out with [AllowAnonymous].
            authorization.DefaultPolicy = new AuthorizationPolicyBuilder()
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
