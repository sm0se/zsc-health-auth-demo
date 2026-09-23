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
// Every ZSC service calls AddZscPlatformAuth in its Program.cs and gets the same
// bearer, subscription-key, and path-selected smart schemes. The fallback policy
// requires an authenticated user on every endpoint which does not explicitly opt out.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = "SubscriptionKey";
    public const string SmartScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ZscOAuth2Options.FromConfiguration(configuration);
        var subscriptionKeyValidator = new SubscriptionKeyValidator(configuration.GetSection("Zsc:SubscriptionKeys").Get<string[]>() ?? Array.Empty<string>());
        services.AddSingleton(options);
        services.AddSingleton(subscriptionKeyValidator);

        services.AddAuthentication(authentication =>
        {
            authentication.DefaultScheme = SmartScheme;
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
            .AddScheme<AuthenticationSchemeOptions, SubscriptionKeyAuthenticationHandler>(SubscriptionKeyScheme, _ => { })
            .AddPolicyScheme(SmartScheme, SmartScheme, policy =>
            {
                policy.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(ZscHeaders.SubscriptionKey) && HealthStatusRoutePredicate.IsHealthStatusRoute(context.Request.Path)
                        ? SubscriptionKeyScheme
                        : OAuth2Scheme;
            });

        services.AddAuthorization(authorization =>
        {
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
