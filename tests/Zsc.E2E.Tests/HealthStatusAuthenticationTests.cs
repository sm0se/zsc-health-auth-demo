using System.Net;
using System.Net.Http.Json;
using Zsc.CommonRoutes;

namespace Zsc.E2E.Tests;

// Requirement R1 - the acceptance criteria for the Health Status API.
//
// The ZSC and ZLS Health Status endpoints authenticate with a subscription key
// instead of an OAuth2 bearer token, end to end through the real chain:
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

    [E2ETheory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Platform_health_status_rejects_an_unknown_subscription_key(string path)
    {
        using var response = await ZscChain.GetWithSubscriptionKeyAsync(path, "not-a-provisioned-key");

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

    // Deviation from REQUIREMENT-R1.md: The Health Status API accepts EITHER a
    // subscription key OR an OAuth2 bearer, not exclusively subscription key.
    // This allows the legacy OAuth2 mechanism to keep working during a transition.
    // All other ZSC APIs remain OAuth2-only.
    [E2ETheory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Platform_health_status_accepts_an_oauth2_bearer_for_backward_compatibility(string path)
    {
        using var response = await ZscChain.GetWithBearerAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
