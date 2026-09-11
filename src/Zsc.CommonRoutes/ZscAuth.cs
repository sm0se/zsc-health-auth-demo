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
// the same thing: the OAuth2 bearer scheme, the subscription-key scheme
// (Requirement R1), a policy scheme that picks between them per request, and an
// authorization FallbackPolicy that requires an authenticated user on every
// endpoint which does not explicitly opt out.
//
// The per-request choice is 'ZscSmart' (AddPolicyScheme): a subscription key is
// only eligible on the Health Status API's own routes (ZscHealthRoutePredicate)
// - everywhere else in the platform stays OAuth2-only, per R1.3. This is the
// one deviation from the requirement doc's hard cutover: on health-status
// routes the two schemes coexist, so a caller may present either credential
// (see docs/REQUIREMENT-R1.md, "Decisions taken", and docs/CHANGES-R1.md for
// why). No service Program.cs calls AddAuthentication/AddJwtBearer itself -
// this is the only place authentication is wired up.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = SubscriptionKeyAuthenticationHandler.SchemeName;
    public const string SmartScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var oauth2Options = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(oauth2Options);

        var subscriptionKeyOptions = ZscSubscriptionKeyOptions.FromConfiguration(configuration);
        services.AddSingleton(subscriptionKeyOptions);
        services.AddSingleton(new SubscriptionKeyValidator(subscriptionKeyOptions.ValidKeys));

        services.AddAuthentication(SmartScheme)
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
            .AddScheme<AuthenticationSchemeOptions, SubscriptionKeyAuthenticationHandler>(SubscriptionKeyScheme, _ => { })
            .AddPolicyScheme(SmartScheme, SmartScheme, policy =>
            {
                policy.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(ZscHeaders.SubscriptionKey)
                    && ZscHealthRoutePredicate.IsHealthStatusRoute(context.Request.Path)
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
