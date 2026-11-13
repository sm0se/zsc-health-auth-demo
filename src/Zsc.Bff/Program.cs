using Zsc.CommonRoutes;

// ZSC BFF service.
//
// Resolves the inbound public path against the common route table and forwards
// it to whichever service owns it. Like every other service in the platform it
// installs AddZscPlatformAuth, so it validates the caller's bearer token again
// for itself rather than trusting the Interceptor to have done it.
//
//     api-gateway -> API Interceptor service -> [ BFF service ] -> Common routes -> downstream API

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ZscServiceRegistry>();
builder.Services.AddZscPlatformAuth(builder.Configuration);
builder.Services.AddTransient<TokenForwardingHandler>();
builder.Services.AddScoped<ZscForwarder>();

foreach (var serviceName in new[] { ZscRoutes.HealthStatusService, ZscRoutes.DeviceApiService })
{
    builder.Services.AddHttpClient(serviceName, (sp, client) =>
            client.BaseAddress = new Uri(sp.GetRequiredService<ZscServiceRegistry>().Resolve(serviceName)))
        .AddHttpMessageHandler<TokenForwardingHandler>();
}

var app = builder.Build();

app.UseZscPlatformAuth();

app.MapZscLiveness("bff");

// Subscription-key routes are not gated here either; the owning service checks the
// key. Each one resolves through the common route table to its downstream.
foreach (var route in ZscRoutes.All.Where(r => r.SubscriptionKey))
{
    app.MapGet(route.PublicPathTemplate, async (HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
        await forwarder.ForwardAsync(context, route.DownstreamService, $"{route.DownstreamPathTemplate}{context.Request.QueryString}", cancellationToken))
        .AllowAnonymous();
}

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
});

app.Run();

public partial class Program { }
