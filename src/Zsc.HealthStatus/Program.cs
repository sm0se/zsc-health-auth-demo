using Zsc.CommonRoutes;
using Zsc.HealthStatus;

// ZSC HealthcheckStatus API - the Health Status API for the ZSC and ZLS
// platforms.
//
// It installs AddZscPlatformAuth like every other ZSC service, so both platform
// endpoints require a valid OAuth2 bearer token, forwarded down from the edge.
//
//     api-gateway -> API Interceptor service -> BFF service -> Common routes -> [ HealthcheckStatus API ]

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ZscServiceRegistry>();
builder.Services.AddZscPlatformAuth(builder.Configuration);
builder.Services.AddScoped<PlatformHealthProbe>();

// Liveness probes of the components this API reports on. No caller context is
// attached: /healthz is anonymous everywhere in the platform.
builder.Services.AddHttpClient(ZscRoutes.DeviceApiService, (sp, client) =>
{
    client.BaseAddress = new Uri(sp.GetRequiredService<ZscServiceRegistry>().Resolve(ZscRoutes.DeviceApiService));
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.UseZscPlatformAuth();

app.MapZscLiveness("health-status");

app.MapGet("/internal/health/zsc/status", async (PlatformHealthProbe probe, CancellationToken cancellationToken) =>
    Results.Ok(await probe.ProbeAsync("zsc", cancellationToken)));

app.MapGet("/internal/health/zls/status", async (PlatformHealthProbe probe, CancellationToken cancellationToken) =>
    Results.Ok(await probe.ProbeAsync("zls", cancellationToken)));

app.Run();

public partial class Program { }
