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
// Every ZSC service calls AddZscPlatformAuth in its Program.cs. After R1, the
// platform uses dual authentication:
//
// - Health-status endpoints (/api/v1/health/* and /internal/health/*) accept
//   EITHER a subscription key (Ocp-Apim-Subscription-Key header) OR an OAuth2
//   bearer token (Authorization: Bearer header).
// - All other endpoints require OAuth2 bearer tokens only.
//
// This is achieved via three authentication schemes:
// 1. JwtBearer (standard OAuth2)
// 2. SubscriptionKey (the new handler)
// 3. ZscSmart (a policy scheme whose ForwardDefaultSelector chooses between them)
//
// The ForwardDefaultSelector examines the request path and headers:
// - If the request carries an Ocp-Apim-Subscription-Key header AND the path is a
//   health-status route, try SubscriptionKey first.
// - Otherwise, try Bearer (OAuth2).
//
// ZscSmart is the DefaultAuthenticateScheme and DefaultChallengeScheme, so it is
// the scheme every endpoint uses unless explicitly overridden. FallbackPolicy
// remains RequireAuthenticatedUser, so /healthz must opt out explicitly.
public static class ZscAuth
{
    public const string OAuth2Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string SubscriptionKeyScheme = SubscriptionKeyAuthenticationHandler.Scheme;
    public const string SmartPolicyScheme = "ZscSmart";

    public static IServiceCollection AddZscPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var oauthOptions = ZscOAuth2Options.FromConfiguration(configuration);
        services.AddSingleton(oauthOptions);

        // Read subscription keys from configuration.
        var subscriptionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var subscriptionKeysSection = configuration.GetSection("Zsc:SubscriptionKeys");
            if (subscriptionKeysSection.Exists())
            {
                var keysArray = subscriptionKeysSection.Get<string[]>();
                if (keysArray != null)
                {
                    foreach (var key in keysArray)
                    {
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            subscriptionKeys.Add(key.Trim());
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log and continue with empty subscription keys if there's a config parsing issue.
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to read subscription keys from configuration: {ex.Message}");
        }

        // Register three authentication schemes:
        // 1. OAuth2 bearer (unchanged)
        // 2. Subscription key (new)
        // 3. Policy scheme to select between them (new)
        services.AddAuthentication(SmartPolicyScheme)
            .AddJwtBearer(OAuth2Scheme, jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = oauthOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = oauthOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(oauthOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            })
            .AddScheme<SubscriptionKeyOptions, SubscriptionKeyAuthenticationHandler>(
                SubscriptionKeyScheme,
                options =>
                {
                    options.ValidKeys = subscriptionKeys;
                })
            .AddPolicyScheme(SmartPolicyScheme, "OAuth2 or SubscriptionKey", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // If this is a health-status route and the request carries a
                    // subscription key header, try the subscription key scheme first.
                    var path = context.Request.Path.Value ?? "";
                    var hasSubscriptionKeyHeader = context.Request.Headers.ContainsKey(ZscHeaders.SubscriptionKey);

                    if (ZscHealthStatusRoutes.IsHealthStatusRoute(path) && hasSubscriptionKeyHeader)
                    {
                        return SubscriptionKeyScheme;
                    }

                    // Otherwise use OAuth2 bearer.
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
