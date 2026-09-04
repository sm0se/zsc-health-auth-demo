using Zsc.CommonRoutes;

// ZSC device API.
//
// A representative "other ZSC API": it is here so that a change to the Health
// Status API can be shown not to have touched anything else. Whatever happens to
// health status, this endpoint stays OAuth2-authenticated.
//
//     api-gateway -> API Interceptor service -> BFF service -> Common routes -> [ device API ]

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddZscPlatformAuthWithFallback(builder.Configuration);

var app = builder.Build();

app.UseZscPlatformAuth();

app.MapZscLiveness("device-api");

app.MapGet("/internal/devices/{deviceId}/status", (string deviceId) => Results.Ok(new DeviceStatusDto(
    DeviceId: deviceId,
    Status: "online",
    Firmware: "4.2.1",
    LastSeenUtc: DateTimeOffset.UtcNow.AddMinutes(-3))));

app.Run();

public partial class Program { }
