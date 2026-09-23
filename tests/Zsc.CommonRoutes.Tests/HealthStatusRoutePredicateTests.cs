using Microsoft.AspNetCore.Http;
using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class HealthStatusRoutePredicateTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    public void Identifies_health_status_routes(string path)
    {
        Assert.True(HealthStatusRoutePredicate.IsHealthStatusRoute(new PathString(path)));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/internal/devices/dev-0001/status")]
    [InlineData("/healthz")]
    [InlineData("/api/v1/health")]
    [InlineData("/internal/health")]
    public void Does_not_identify_other_paths(string path)
    {
        Assert.False(HealthStatusRoutePredicate.IsHealthStatusRoute(new PathString(path)));
    }
}
