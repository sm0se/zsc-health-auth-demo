using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zsc.CommonRoutes;

public sealed class SubscriptionKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string HeaderName = "Ocp-Apim-Subscription-Key";
}

// Authenticates a caller by the `Ocp-Apim-Subscription-Key` header instead of
// an OAuth2 bearer. Used only on the Health Status API's routes - see
// ZscAuth's 'ZscSmart' policy scheme, which is what decides whether a given
// request is routed to this handler or to the Bearer one.
//
// A missing header is NoResult, not Fail: it lets a request with no
// subscription key but a valid bearer (or vice versa on a route that accepts
// either) fall through to whichever scheme actually applies, rather than
// forcing a 401 from this handler alone. A present-but-wrong key is a hard
// Fail - the caller attempted this scheme and did not satisfy it.
public sealed class SubscriptionKeyAuthenticationHandler(
    IOptionsMonitor<SubscriptionKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SubscriptionKeyValidator validator)
    : AuthenticationHandler<SubscriptionKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerValue = Request.Headers[SubscriptionKeyAuthenticationOptions.HeaderName].ToString();
        var result = validator.Validate(headerValue);

        return Task.FromResult(result switch
        {
            SubscriptionKeyValidationResult.Missing => AuthenticateResult.NoResult(),
            SubscriptionKeyValidationResult.Invalid => AuthenticateResult.Fail("Unknown subscription key."),
            SubscriptionKeyValidationResult.Valid => AuthenticateResult.Success(BuildTicket(headerValue)),
            _ => AuthenticateResult.NoResult(),
        });
    }

    private AuthenticationTicket BuildTicket(string headerValue)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.AuthenticationMethod, "SubscriptionKey") },
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, Scheme.Name);
    }
}
