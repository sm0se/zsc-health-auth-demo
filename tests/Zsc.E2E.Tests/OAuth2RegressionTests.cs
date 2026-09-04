using System.Net;
using System.Net.Http.Json;
using Zsc.CommonRoutes;

namespace Zsc.E2E.Tests;

// Requirement R1.3 - "All other ZSC APIs shall remain OAuth2-authenticated."
//
// These tests pass on the unmodified repository and must still pass afterwards.
// They are what stops the health-status change from becoming a platform-wide one.
public class OAuth2RegressionTests
{
    private const string DeviceStatusPath = "/api/v1/devices/dev-0001/status";

    [E2EFact]
    public async Task Other_zsc_apis_are_served_against_an_oauth2_bearer()
    {
        using var response = await ZscChain.GetWithBearerAsync(DeviceStatusPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DeviceStatusDto>();
        Assert.NotNull(body);
        Assert.Equal("dev-0001", body!.DeviceId);
    }

    [E2EFact]
    public async Task Other_zsc_apis_do_not_accept_a_subscription_key()
    {
        using var response = await ZscChain.GetWithSubscriptionKeyAsync(DeviceStatusPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [E2EFact]
    public async Task Other_zsc_apis_reject_a_caller_with_no_credentials()
    {
        using var response = await ZscChain.GetAsync(DeviceStatusPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [E2EFact]
    public async Task Gateway_liveness_stays_anonymous()
    {
        using var response = await ZscChain.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
