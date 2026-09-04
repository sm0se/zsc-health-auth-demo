using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.Interceptor.Tests;

// The Interceptor is the platform's authentication boundary: these tests pin
// what it rejects. Most endpoints require OAuth2, but health endpoints are
// passed through anonymously to allow downstream services to apply alternate
// authentication schemes (e.g. subscription keys). What the Interceptor does
// with accepted requests - forwarding to the BFF - needs the rest of the chain
// running and belongs in tests/Zsc.E2E.Tests.
public class InterceptorAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InterceptorAuthTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Liveness_is_anonymous()
    {
        var response = await _factory.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    public async Task Non_health_requests_without_credentials_are_rejected(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Health_requests_without_credentials_are_forwarded_for_downstream_auth(string path)
    {
        // Health endpoints at the Interceptor are allowed anonymously; the
        // downstream service (HealthStatus via BFF) will handle authentication.
        // We get BadGateway here because there's no downstream to talk to in
        // in-process tests, but the request makes it through the Interceptor.
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    public async Task Non_health_requests_with_an_unusable_bearer_token_are_rejected(string path)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
