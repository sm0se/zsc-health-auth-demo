using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class ZscHealthStatusRouteTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    [InlineData("/API/V1/HEALTH/zsc/status")] // the predicate is case-insensitive on the path itself
    public void Health_status_paths_are_recognised(string path)
    {
        Assert.True(ZscHealthStatusRoute.IsHealthStatusRoute(path));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/internal/devices/dev-0001/status")]
    [InlineData("/healthz")]
    [InlineData("")]
    public void Other_paths_are_not_health_status_routes(string path)
    {
        Assert.False(ZscHealthStatusRoute.IsHealthStatusRoute(path));
    }
}
