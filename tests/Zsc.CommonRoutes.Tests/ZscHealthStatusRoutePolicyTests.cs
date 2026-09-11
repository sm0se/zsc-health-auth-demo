using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class ZscHealthStatusRoutePolicyTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/api/v1/health/")]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    [InlineData("/internal/health/")]
    public void Public_and_internal_health_prefixes_are_health_status_routes(string path)
    {
        Assert.True(ZscHealthStatusRoutePolicy.IsHealthStatusRoute(path));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/api/v1/healthzzz")]
    [InlineData("/internal/devices/dev-0001/status")]
    [InlineData("/healthz")]
    [InlineData("")]
    public void Other_paths_are_not_health_status_routes(string path)
    {
        Assert.False(ZscHealthStatusRoutePolicy.IsHealthStatusRoute(path));
    }

    [Fact]
    public void The_check_is_case_insensitive_on_the_path()
    {
        Assert.True(ZscHealthStatusRoutePolicy.IsHealthStatusRoute("/API/V1/HEALTH/zsc/status"));
        Assert.True(ZscHealthStatusRoutePolicy.IsHealthStatusRoute("/INTERNAL/HEALTH/zsc/status"));
    }
}
