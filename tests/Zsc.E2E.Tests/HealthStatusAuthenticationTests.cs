using System.Net;
using System.Net.Http.Json;
using Zsc.CommonRoutes;

namespace Zsc.E2E.Tests;

// Requirement R1 - the acceptance criteria for the Health Status API.
//
// The ZSC and ZLS Health Status endpoints authenticate with a subscription key
// end to end through the real chain:
//
//     api-gateway -> API Interceptor service -> BFF service -> Common routes -> HealthcheckStatus API
//
// These tests fail on the unmodified repository. They are the definition of done.
public class HealthStatusAuthenticationTests
{
    [E2ETheory]
    [InlineData("/api/v1/health/zsc/status", "ZSC")]
    [InlineData("/api/v1/health/zls/status", "ZLS")]
    public async Task Platform_health_status_is_served_against_a_valid_subscription_key(string path, string platform)
    {
        using var response = await ZscChain.GetWithSubscriptionKeyAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthStatusDto>();
        Assert.NotNull(body);
        Assert.Equal(platform, body!.Platform);
        Assert.NotEmpty(body.Components);
    }

    // "wrong-key-000" is the wrong-key case from the acceptance criteria.
    [E2ETheory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Platform_health_status_rejects_an_unknown_subscription_key(string path)
    {
        using var response = await ZscChain.GetWithSubscriptionKeyAsync(path, "wrong-key-000");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [E2ETheory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Platform_health_status_rejects_a_caller_with_no_credentials(string path)
    {
        using var response = await ZscChain.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R1 is implemented as a deliberate deviation from the doc's hard cutover:
    // the legacy OAuth2 method keeps working on the Health Status API so that
    // existing bearer-only callers are not broken while the subscription-key
    // method is adopted. See docs/CHANGES-R1.md, "Coexistence policy".
    [E2ETheory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Platform_health_status_still_accepts_an_oauth2_bearer_alone(string path)
    {
        using var response = await ZscChain.GetWithBearerAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
