using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.Interceptor.Tests;

// The Interceptor is the platform's authentication boundary: these tests pin
// what it rejects. Most endpoints require OAuth2; health endpoints are exempt
// from this requirement to allow downstream services to apply alternate auth
// (e.g. subscription keys). The actual auth behavior through the whole chain
// is tested by the E2E suite.
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
    [InlineData("/api/v1/devices/dev-0001/status")]
    public async Task Non_health_requests_with_an_unusable_bearer_token_are_rejected(string path)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
