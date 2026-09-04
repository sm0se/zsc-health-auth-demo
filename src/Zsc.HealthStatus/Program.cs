using Microsoft.AspNetCore.Authorization;
using Zsc.CommonRoutes;
using Zsc.HealthStatus;

// ZSC HealthcheckStatus API - the Health Status API for the ZSC and ZLS
// platforms.
//
// It uses per-route authorization policies: the health status endpoints require
// a valid subscription key (not OAuth2 bearer), while other endpoints opt out
// of authentication.
//
//     api-gateway -> API Interceptor service -> BFF service -> Common routes -> [ HealthcheckStatus API ]

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ZscServiceRegistry>();
builder.Services.AddZscPlatformAuthWithPerRoutePolicy(builder.Configuration);
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

// Both health status endpoints require subscription key authentication.
app.MapGet("/internal/health/zsc/status", async (PlatformHealthProbe probe, CancellationToken cancellationToken) =>
    Results.Ok(await probe.ProbeAsync("zsc", cancellationToken)))
    .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ZscSubscriptionKeyAuth.SchemeName });

app.MapGet("/internal/health/zls/status", async (PlatformHealthProbe probe, CancellationToken cancellationToken) =>
    Results.Ok(await probe.ProbeAsync("zls", cancellationToken)))
    .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ZscSubscriptionKeyAuth.SchemeName });

app.Run();

public partial class Program { }
