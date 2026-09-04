using Zsc.ApiGateway;
using Zsc.CommonRoutes;

// ZSC API gateway - the platform's public front door.
//
// It authenticates nothing itself: it narrows inbound headers, stamps a
// correlation id, and hands the request to the API Interceptor service, which is
// where the platform's authentication policy is enforced.
//
//     [ api-gateway ] -> API Interceptor service -> BFF service -> Common routes -> downstream API

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ZscServiceRegistry>();
builder.Services.AddSingleton(ZscOAuth2Options.FromConfiguration(builder.Configuration));
builder.Services.AddTransient<TokenForwardingHandler>();
builder.Services.AddScoped<ZscForwarder>();

builder.Services.AddHttpClient("interceptor", (sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<ZscServiceRegistry>().Resolve("interceptor")))
    .AddHttpMessageHandler<TokenForwardingHandler>();

var app = builder.Build();

app.UseMiddleware<EdgeHeaderPolicyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapZscLiveness("api-gateway");

// Stands in for the tenant's authorization server so the demo can obtain a
// bearer token without one. Disabled unless Zsc:EnableDevTokenEndpoint is set.
if (builder.Configuration.GetValue("Zsc:EnableDevTokenEndpoint", false))
{
    app.MapPost("/dev/token", (ZscOAuth2Options options) => Results.Ok(new
    {
        access_token = DevTokenIssuer.Issue(options),
        token_type = "Bearer",
        expires_in = 3600,
    })).AllowAnonymous();
}

app.MapGet("/api/v1/{**rest}", async (string rest, HttpContext context, ZscForwarder forwarder, CancellationToken cancellationToken) =>
    await forwarder.ForwardAsync(context, "interceptor", $"/api/v1/{rest}{context.Request.QueryString}", cancellationToken));

app.Run();

public partial class Program { }
