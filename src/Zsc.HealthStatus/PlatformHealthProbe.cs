using System.Diagnostics;
using Zsc.CommonRoutes;

namespace Zsc.HealthStatus;

// Builds the Health Status payload for one platform by probing the components
// that platform is composed of. The device registry is probed for real over its
// liveness endpoint; the remaining components are stubbed for the demo.
public sealed class PlatformHealthProbe(IHttpClientFactory httpClientFactory, ILogger<PlatformHealthProbe> logger)
{
    private const string Version = "2026.9.0";

    public async Task<HealthStatusDto> ProbeAsync(string platform, CancellationToken cancellationToken)
    {
        var components = platform.ToLowerInvariant() switch
        {
            "zsc" => await ZscComponentsAsync(cancellationToken),
            "zls" => Stubbed(("logistics-core", 11), ("shipment-tracker", 24), ("carrier-gateway", 37)),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
        };

        var status = components.All(c => c.Status == "healthy") ? "healthy" : "degraded";
        return new HealthStatusDto(platform.ToUpperInvariant(), status, Version, DateTimeOffset.UtcNow, components);
    }

    private async Task<IReadOnlyList<ComponentHealthDto>> ZscComponentsAsync(CancellationToken cancellationToken)
    {
        var components = new List<ComponentHealthDto>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = httpClientFactory.CreateClient(ZscRoutes.DeviceApiService);
            using var response = await client.GetAsync("/healthz", cancellationToken);
            components.Add(new ComponentHealthDto("device-registry", response.IsSuccessStatusCode ? "healthy" : "degraded", (int)stopwatch.ElapsedMilliseconds));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Device registry liveness probe failed");
            components.Add(new ComponentHealthDto("device-registry", "unreachable", (int)stopwatch.ElapsedMilliseconds));
        }

        components.AddRange(Stubbed(("identity", 8), ("telemetry-ingest", 19)));
        return components;
    }

    private static IReadOnlyList<ComponentHealthDto> Stubbed(params (string Name, int LatencyMs)[] components) =>
        components.Select(c => new ComponentHealthDto(c.Name, "healthy", c.LatencyMs)).ToList();
}
