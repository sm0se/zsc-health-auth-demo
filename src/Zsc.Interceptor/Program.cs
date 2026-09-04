using Microsoft.AspNetCore.Authorization;
using Zsc.CommonRoutes;

// ZSC API Interceptor service.
//
// The platform's authentication boundary. It validates OAuth2 bearer tokens on
// most endpoints but forwards requests to the health status API without requiring
// OAuth2 (they may have subscription keys instead). Requests without credentials
// at all are rejected at the BFF level when they try to reach protected endpoints.
//
//     api-gateway -> [ API Interceptor service ] -> BFF service -> Common routes -> downstream API

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ZscServiceRegistry>();
builder.Services.AddZscPlatformAuth(builder.Configuration);
builder.Services.AddZscSubscriptionKeyAuth(builder.Configuration);
builder.Services.AddTransient<TokenForwardingHandler>();
builder.Services.AddScoped<ZscForwarder>();

builder.Services.AddHttpClient("bff", (sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<ZscServiceRegistry>().Resolve("bff")))
    .AddHttpMessageHandler<TokenForwardingHandler>();

builder.Services.AddAuthorization(authorization =>
{
    // No fallback policy means endpoints without explicit authorization metadata are allowed
    authorization.FallbackPolicy = null;
});

var app = builder.Build();

app.UseZscPlatformAuth();

app.MapZscLiveness("interceptor");

app.MapGet("/api/v1/health/{**rest}", async (string rest, HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
    await forwarder.ForwardAsync(context, "bff", $"/api/v1/health/{rest}{context.Request.QueryString}", cancellationToken))
    .WithName("health-forward");

app.MapGet("/api/v1/{**rest}", async (string rest, HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
    await forwarder.ForwardAsync(context, "bff", $"/api/v1/{rest}{context.Request.QueryString}", cancellationToken))
    .RequireAuthorization();

app.Run();

public partial class Program { }
