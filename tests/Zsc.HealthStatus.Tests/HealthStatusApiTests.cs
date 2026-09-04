using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Zsc.CommonRoutes;

namespace Zsc.HealthStatus.Tests;

// In-process tests of the HealthcheckStatus API's own contract.
//
// These exercise the service in isolation: nothing upstream of it (gateway,
// Interceptor, BFF) is involved, and its downstream component probe has nothing
// to talk to. They pin what this service requires of a caller - not what a
// caller actually experiences through the chain, which is what
// tests/Zsc.E2E.Tests is for.
public class HealthStatusApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthStatusApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient AuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var options = _factory.Services.GetRequiredService<ZscOAuth2Options>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevTokenIssuer.Issue(options));
        return client;
    }

    [Fact]
    public async Task Liveness_is_anonymous()
    {
        var response = await _factory.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    public async Task Platform_status_requires_credentials(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/internal/health/zsc/status", "ZSC")]
    [InlineData("/internal/health/zls/status", "ZLS")]
    public async Task Platform_status_is_served_to_an_authenticated_caller(string path, string platform)
    {
        var response = await AuthenticatedClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthStatusDto>();
        Assert.NotNull(body);
        Assert.Equal(platform, body!.Platform);
        Assert.NotEmpty(body.Components);
    }
}
