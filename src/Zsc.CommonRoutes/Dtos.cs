namespace Zsc.CommonRoutes;

// Payload of the ZSC / ZLS Health Status API.
public sealed record HealthStatusDto(
    string Platform,
    string Status,
    string Version,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<ComponentHealthDto> Components);

public sealed record ComponentHealthDto(string Name, string Status, int LatencyMs);

// Payload of one of the other ZSC APIs, which stay OAuth2-authenticated.
public sealed record DeviceStatusDto(
    string DeviceId,
    string Status,
    string Firmware,
    DateTimeOffset LastSeenUtc);
