using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class ZscHealthStatusPathTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    public void Health_status_paths_are_recognised(string path)
    {
        Assert.True(ZscHealthStatusPath.IsHealthStatusPath(path));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/healthz")]
    [InlineData("/api/v1/health")]
    [InlineData("/internal/health")]
    [InlineData("")]
    public void Other_paths_are_not_health_status_paths(string path)
    {
        Assert.False(ZscHealthStatusPath.IsHealthStatusPath(path));
    }
}
