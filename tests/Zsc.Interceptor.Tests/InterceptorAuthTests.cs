using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Zsc.Interceptor.Tests;

// The Interceptor is the platform's authentication boundary: these tests pin
// what it rejects. What it does with a request it accepts - forwarding to the
// BFF - needs the rest of the chain running and belongs in tests/Zsc.E2E.Tests.
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

    [Fact]
    public async Task Device_requests_without_credentials_are_rejected_before_they_are_forwarded()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/devices/dev-0001/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Device_requests_with_an_unusable_bearer_token_are_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/v1/devices/dev-0001/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R1: health routes are subscription-key routes. The interceptor does not
    // check the key; it forwards the request to BFF, and the owning service
    // (Health Status) decides. No BFF runs in-process, so the forward fails here
    // with 502. The point of this test is that the interceptor itself does not
    // answer 401 for them, whether or not a bearer is sent.
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    public async Task Health_requests_are_not_gated_by_the_interceptor(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
