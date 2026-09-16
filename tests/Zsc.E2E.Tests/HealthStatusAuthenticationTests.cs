using System.Net;
using System.Net.Http.Json;
using Zsc.CommonRoutes;

namespace Zsc.E2E.Tests;

// Requirement R1 - the acceptance criteria for the Health Status API.
//
// The ZSC and ZLS Health Status endpoints accept a subscription key end to end
// through the real chain, and - by the one deviation from a hard cutover this
// repository implements - keep accepting an OAuth2 bearer too:
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

    // R1's one deviation from a hard cutover: the Health Status API keeps
    // accepting an OAuth2 bearer alongside the new subscription key, so the
    // legacy method keeps working while the new one rolls out. See
    // docs/REQUIREMENT-R1.md and docs/CHANGES-R1.md, "Coexistence policy".
    [E2ETheory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Platform_health_status_still_accepts_an_oauth2_bearer_alone(string path)
    {
        using var response = await ZscChain.GetWithBearerAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
