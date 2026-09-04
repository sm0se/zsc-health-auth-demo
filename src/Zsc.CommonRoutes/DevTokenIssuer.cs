using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Zsc.CommonRoutes;

// Mints the bearer tokens the demo authenticates with, using the same symmetric
// key the services validate against.
//
// This stands in for the tenant's authorization server so the chain runs with no
// external dependency. It is used by the gateway's development token endpoint
// and by the test suites; nothing in the request path calls it.
public static class DevTokenIssuer
{
    public static string Issue(
        ZscOAuth2Options options,
        string subject = "zsc-dev-client",
        IEnumerable<string>? scopes = null,
        TimeSpan? lifetime = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("n")),
            new("scope", string.Join(' ', scopes ?? new[] { "zsc.read" })),
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
