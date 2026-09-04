using System.IdentityModel.Tokens.Jwt;
using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class DevTokenIssuerTests
{
    private static readonly ZscOAuth2Options Options =
        new("https://dev-idp.zsc.local/", "zsc-api", "zsc-demo-signing-key-not-a-real-secret-0123456789");

    [Fact]
    public void Issues_a_token_for_the_configured_issuer_and_audience()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(DevTokenIssuer.Issue(Options));

        Assert.Equal(Options.Issuer, token.Issuer);
        Assert.Contains(Options.Audience, token.Audiences);
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }
}
