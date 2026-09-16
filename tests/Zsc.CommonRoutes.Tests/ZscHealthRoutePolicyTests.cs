using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class ZscHealthRoutePolicyTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    public void Health_status_routes_are_recognised(string path)
    {
        Assert.True(ZscHealthRoutePolicy.IsHealthStatusRoute(path));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/healthz")]
    [InlineData("/api/v1/health")]
    [InlineData("/dev/token")]
    public void Non_health_status_routes_are_not_recognised(string path)
    {
        Assert.False(ZscHealthRoutePolicy.IsHealthStatusRoute(path));
    }

    [Fact]
    public void The_check_is_case_insensitive()
    {
        Assert.True(ZscHealthRoutePolicy.IsHealthStatusRoute("/API/V1/HEALTH/zsc/status"));
    }
}
