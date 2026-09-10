using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class ZscRoutesTests
{
    [Theory]
    [InlineData("/api/v1/health/zsc/status", "health-zsc", "health-status", "/internal/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status", "health-zls", "health-status", "/internal/health/zls/status")]
    public void Resolves_platform_health_routes(string publicPath, string routeId, string service, string downstreamPath)
    {
        var resolved = ZscRoutes.Resolve(publicPath);

        Assert.NotNull(resolved);
        Assert.Equal(routeId, resolved!.Value.Route.Id);
        Assert.Equal(service, resolved.Value.Route.DownstreamService);
        Assert.Equal(downstreamPath, resolved.Value.DownstreamPath);
    }

    [Fact]
    public void Substitutes_path_parameters_into_the_downstream_path()
    {
        var resolved = ZscRoutes.Resolve("/api/v1/devices/dev-0001/status");

        Assert.NotNull(resolved);
        Assert.Equal("device-status", resolved!.Value.Route.Id);
        Assert.Equal("/internal/devices/dev-0001/status", resolved.Value.DownstreamPath);
    }

    [Fact]
    public void Placeholders_match_a_single_segment_only()
    {
        Assert.Null(ZscRoutes.Resolve("/api/v1/devices/dev/0001/status"));
    }

    [Theory]
    [InlineData("/api/v1/unknown")]
    [InlineData("/api/v1/health/zsc")]
    [InlineData("/health/zsc/status")]
    public void Returns_null_for_paths_that_are_not_registered(string publicPath)
    {
        Assert.Null(ZscRoutes.Resolve(publicPath));
    }

    [Theory]
    [InlineData("/api/v1/health/zsc/status", true)]
    [InlineData("/api/v1/health/zls/status", true)]
    [InlineData("/internal/health/zsc/status", true)]
    [InlineData("/internal/health/zls/status", true)]
    [InlineData("/api/v1/devices/dev-0001/status", false)]
    [InlineData("/api/v1/health", false)]
    [InlineData("/api/v1/healthz", false)]
    [InlineData("/healthz", false)]
    public void IsHealthStatusRoute_correctly_identifies_health_status_paths(string path, bool expected)
    {
        Assert.Equal(expected, ZscRoutes.IsHealthStatusRoute(path));
    }

    [Theory]
    [InlineData("api/v1/health/zsc/status", true)]
    [InlineData("internal/health/zls/status", true)]
    public void IsHealthStatusRoute_normalizes_paths_without_leading_slash(string path, bool expected)
    {
        Assert.Equal(expected, ZscRoutes.IsHealthStatusRoute(path));
    }
}
