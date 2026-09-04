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

    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/api/v1/devices/dev-0001/status")]
    public async Task Requests_without_credentials_are_rejected_before_they_are_forwarded(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/devices/dev-0001/status")]
    public async Task Requests_with_an_unusable_bearer_token_are_rejected(string path)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
