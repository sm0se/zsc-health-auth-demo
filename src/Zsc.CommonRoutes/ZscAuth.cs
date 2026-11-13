using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Zsc.CommonRoutes;

// Platform authentication.
//
// Every ZSC service calls AddZscPlatformAuth in its Program.cs: the OAuth2 bearer
// scheme, plus an authorization FallbackPolicy that requires an authenticated
// user on every endpoint which does not explicitly opt out. That fallback is the
// default for the whole platform.
//
// The one exception is the Health Status API. A service that owns such routes
// also calls AddZscSubscriptionKey, which adds the Ocp-Apim-Subscription-Key
// scheme and a named policy (SubscriptionKeyPolicy). Endpoints opt in with
// RequireAuthorization(SubscriptionKeyPolicy), and that policy evaluates only the
// key scheme, so a bearer token never opens those endpoints. Which routes use
// the key is recorded once in ZscRoutes (ZscRoute.SubscriptionKey).
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = "SubscriptionKey";
    public const string SubscriptionKeyPolicy = "SubscriptionKey";

    public static IServiceCollection AddZscSubscriptionKey(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(ZscSubscriptionKeys.FromConfiguration(configuration));

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, SubscriptionKeyAuthHandler>(SubscriptionKeyScheme, _ => { });

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(SubscriptionKeyPolicy, policy => policy
                .AddAuthenticationSchemes(SubscriptionKeyScheme)
                .RequireAuthenticatedUser());
        });

        return services;
    }

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

        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder(OAuth2Scheme)
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
