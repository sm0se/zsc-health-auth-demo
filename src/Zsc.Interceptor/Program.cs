using Zsc.CommonRoutes;

// ZSC API Interceptor service.
//
// The platform's authentication boundary. AddZscPlatformAuth installs the OAuth2
// bearer scheme together with an authorization FallbackPolicy, so every endpoint
// this process serves - including every path it forwards - requires a valid
// bearer token. A request that reaches here without one is rejected with 401 and
// never touches the BFF.
//
//     api-gateway -> [ API Interceptor service ] -> BFF service -> Common routes -> downstream API

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ZscServiceRegistry>();
builder.Services.AddZscPlatformAuth(builder.Configuration);
builder.Services.AddTransient<TokenForwardingHandler>();
builder.Services.AddScoped<ZscForwarder>();

builder.Services.AddHttpClient("bff", (sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<ZscServiceRegistry>().Resolve("bff")))
    .AddHttpMessageHandler<TokenForwardingHandler>();

var app = builder.Build();

app.UseZscPlatformAuth();

app.MapZscLiveness("interceptor");

// Subscription-key routes are not gated here: the key is validated by the service
// that owns the route (Health Status). The literal paths take precedence over the
// OAuth2-protected catch-all below.
foreach (var route in ZscRoutes.All.Where(r => r.SubscriptionKey))
{
    app.MapGet(route.PublicPathTemplate, async (HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
        await forwarder.ForwardAsync(context, "bff", $"{context.Request.Path}{context.Request.QueryString}", cancellationToken))
        .AllowAnonymous();
}

app.MapGet("/api/v1/{**rest}", async (string rest, HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
    await forwarder.ForwardAsync(context, "bff", $"/api/v1/{rest}{context.Request.QueryString}", cancellationToken));

app.Run();

public partial class Program { }
