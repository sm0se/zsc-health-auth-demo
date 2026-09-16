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
// the same thing, plus an authorization FallbackPolicy that requires an
// authenticated user on every endpoint which does not explicitly opt out.
// Authentication is a process-wide decision made once at startup - there is no
// per-route policy anywhere in the platform, and no service declares which of
// its endpoints need which scheme.
//
// R1 adds one deviation from a hard cutover: the Health Status API accepts
// EITHER a subscription key OR an OAuth2 bearer, so the legacy method keeps
// working while the new one is rolled out. Every other ZSC API stays
// OAuth2-only. Three schemes are registered in every service - 'Bearer',
// 'SubscriptionKey', and the policy scheme 'ZscSmart' that picks between them -
// so the decision of which scheme to run lives in one place (this class) and
// no service's Program.cs calls AddAuthentication/AddJwtBearer itself.
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
                    context.Request.Headers.ContainsKey(ZscHeaders.SubscriptionKey) &&
                    ZscHealthStatusPath.IsHealthStatusPath(context.Request.Path)
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
