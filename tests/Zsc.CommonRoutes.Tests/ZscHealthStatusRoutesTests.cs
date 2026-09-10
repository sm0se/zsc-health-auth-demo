using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

// Unit tests for health-status route identification.
//
// These tests verify that the route predicate correctly identifies
// health-status endpoints that require special authentication handling.
public class ZscHealthStatusRoutesTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/api/v1/health/zsc/status?param=value")]
    [InlineData("/API/V1/HEALTH/ZSC/STATUS")] // case-insensitive
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    public void Identifies_health_status_routes(string path)
    {
        Assert.True(ZscHealthStatusRoutes.IsHealthStatusRoute(path));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/api/v1/unknown")]
    [InlineData("/healthz")]
    [InlineData("/internal/devices/dev-0001/status")]
    [InlineData("/internal/other/path")]
    public void Does_not_identify_non_health_status_routes(string path)
    {
        Assert.False(ZscHealthStatusRoutes.IsHealthStatusRoute(path));
    }
}
