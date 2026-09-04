using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

// OAuth2 bearer validation settings, read from the `Zsc:OAuth2` configuration
// section by every service in the platform.
//
// The demo signs with a symmetric key so the whole chain runs offline with no
// identity provider to stand up. A real deployment would point at the tenant's
// authorization server metadata instead; nothing else about the shape changes.
public sealed record ZscOAuth2Options(string Issuer, string Audience, string SigningKey)
{
    public const string SectionName = "Zsc:OAuth2";

    public static ZscOAuth2Options FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var issuer = section["Issuer"];
        var audience = section["Audience"];
        var signingKey = section["SigningKey"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                $"Configuration section '{SectionName}' must define Issuer, Audience and SigningKey.");
        }

        return new ZscOAuth2Options(issuer, audience, signingKey);
    }
}
