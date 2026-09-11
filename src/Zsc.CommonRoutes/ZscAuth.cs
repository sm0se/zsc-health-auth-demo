using System.Text;
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
// Every ZSC service calls AddZscPlatformAuth in its Program.cs and gets exactly
// the same thing: the OAuth2 bearer scheme, the subscription-key scheme, a
// policy scheme ('ZscSmart') that picks between them per request, plus an
// authorization FallbackPolicy that requires an authenticated user on every
// endpoint which does not explicitly opt out. Authentication is a process-wide
// decision made once at startup - no service declares which of its endpoints
// need which scheme; the policy scheme's selector is the single place that
// distinguishes the Health Status API's routes from everything else (see
// ZscHealthStatusRoute). This is Requirement R1's one deviation from a hard
// cutover: on Health Status routes, a subscription key OR a bearer opens the
// door; everywhere else, only a bearer does.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = "SubscriptionKey";
    public const string SmartScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(options);
        services.AddSingleton<SubscriptionKeyValidator>();

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
            .AddScheme<SubscriptionKeyAuthenticationOptions, SubscriptionKeyAuthenticationHandler>(
                SubscriptionKeyScheme, _ => { })
            .AddPolicyScheme(SmartScheme, SmartScheme, policy =>
            {
                policy.ForwardDefaultSelector = ForwardDefaultSelector;
            });

        services.AddAuthorization(authorization =>
        {
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder(SmartScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    // The subscription key is only a valid way in on the Health Status API's
    // routes; everywhere else it is not even considered, so a caller who sends
    // both a bearer and a (wrong) key on e.g. /api/v1/devices/... is judged on
    // the bearer alone.
    private static string ForwardDefaultSelector(HttpContext context)
    {
        var carriesSubscriptionKey = context.Request.Headers.ContainsKey(SubscriptionKeyAuthenticationOptions.HeaderName);
        var isHealthStatusRoute = ZscHealthStatusRoute.IsHealthStatusRoute(context.Request.Path.Value ?? string.Empty);

        return carriesSubscriptionKey && isHealthStatusRoute
            ? SubscriptionKeyScheme
            : OAuth2Scheme;
    }

    public static WebApplication UseZscPlatformAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
