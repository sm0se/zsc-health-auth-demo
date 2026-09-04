using Microsoft.AspNetCore.Authorization;
using Zsc.CommonRoutes;

// ZSC BFF service.
//
// Resolves the inbound public path against the common route table and forwards
// it to whichever service owns it. Most endpoints require OAuth2 bearer tokens,
// but health status endpoints allow subscription keys instead. It enforces
// per-route policies: health paths allow anonymous (BFF doesn't validate auth
// here, deferring to the downstream HealthStatus service), while other paths
// require OAuth2.
//
//     api-gateway -> API Interceptor service -> [ BFF service ] -> Common routes -> downstream API

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ZscServiceRegistry>();
builder.Services.AddZscPlatformAuth(builder.Configuration);
builder.Services.AddZscSubscriptionKeyAuth(builder.Configuration);
builder.Services.AddTransient<TokenForwardingHandler>();
builder.Services.AddScoped<ZscForwarder>();

foreach (var serviceName in new[] { ZscRoutes.HealthStatusService, ZscRoutes.DeviceApiService })
{
    builder.Services.AddHttpClient(serviceName, (sp, client) =>
            client.BaseAddress = new Uri(sp.GetRequiredService<ZscServiceRegistry>().Resolve(serviceName)))
        .AddHttpMessageHandler<TokenForwardingHandler>();
}

builder.Services.AddAuthorization(authorization =>
{
    authorization.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseZscPlatformAuth();

app.MapZscLiveness("bff");

app.MapGet("/api/v1/health/{**rest}", async (string rest, HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
{
    var publicPath = $"/api/v1/health/{rest}";
    var resolved = ZscRoutes.Resolve(publicPath);
    if (resolved is null)
    {
        return Results.NotFound(new { error = $"No common route is registered for '{publicPath}'." });
    }

    var (route, downstreamPath) = resolved.Value;
    return await forwarder.ForwardAsync(context, route.DownstreamService, $"{downstreamPath}{context.Request.QueryString}", cancellationToken);
})
.AllowAnonymous();

app.MapGet("/api/v1/{**rest}", async (string rest, HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
{
    var publicPath = $"/api/v1/{rest}";
    var resolved = ZscRoutes.Resolve(publicPath);
    if (resolved is null)
    {
        return Results.NotFound(new { error = $"No common route is registered for '{publicPath}'." });
    }

    var (route, downstreamPath) = resolved.Value;
    return await forwarder.ForwardAsync(context, route.DownstreamService, $"{downstreamPath}{context.Request.QueryString}", cancellationToken);
})
.RequireAuthorization();

app.Run();

public partial class Program { }
