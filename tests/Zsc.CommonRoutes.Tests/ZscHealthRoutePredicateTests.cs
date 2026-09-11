using Microsoft.AspNetCore.Http;
using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class ZscHealthRoutePredicateTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    [InlineData("/API/V1/HEALTH/zsc/status")]
    public void Health_status_paths_are_recognised(string path)
    {
        Assert.True(ZscHealthRoutePredicate.IsHealthStatusRoute(new PathString(path)));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/internal/devices/dev-0001/status")]
    [InlineData("/healthz")]
    [InlineData("/")]
    public void Other_paths_are_not_recognised(string path)
    {
        Assert.False(ZscHealthRoutePredicate.IsHealthStatusRoute(new PathString(path)));
    }
}
