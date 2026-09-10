using System.Text;
using System.Text.RegularExpressions;

namespace Zsc.CommonRoutes;

// One public route and the downstream it resolves to. `PublicPathTemplate` uses
// `{name}` placeholders; whatever they capture is substituted into
// `DownstreamPathTemplate`.
public sealed record ZscRoute(string Id, string PublicPathTemplate, string DownstreamService, string DownstreamPathTemplate);

// The common route table.
//
// Every public ZSC path is declared here once, together with the service that
// ultimately answers it. The Interceptor forwards by prefix without consulting
// this table; the BFF resolves against it to find the downstream service. This
// is "Common routes" in the platform's request chain:
//
//     API Interceptor service -> BFF service -> Common routes -> downstream API
public static class ZscRoutes
{
    // A path is a health-status route if it is a path to the Health Status API.
    // These are authentication-sensitive: they accept subscription keys instead of
    // (or in addition to) OAuth2 bearer tokens.
    //
    // Public paths (as seen at the gateway):
    //   /api/v1/health/zsc/status
    //   /api/v1/health/zls/status
    //
    // Internal paths (as seen inside the platform):
    //   /internal/health/zsc/status
    //   /internal/health/zls/status
    public static bool IsHealthStatusRoute(string path)
    {
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        return normalizedPath.StartsWith("/api/v1/health/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("/internal/health/", StringComparison.OrdinalIgnoreCase);
    }
    public const string HealthStatusService = "health-status";
    public const string DeviceApiService = "device-api";

    public static readonly IReadOnlyList<ZscRoute> All = new[]
    {
        new ZscRoute("health-zsc", "/api/v1/health/zsc/status", HealthStatusService, "/internal/health/zsc/status"),
        new ZscRoute("health-zls", "/api/v1/health/zls/status", HealthStatusService, "/internal/health/zls/status"),
        new ZscRoute("device-status", "/api/v1/devices/{deviceId}/status", DeviceApiService, "/internal/devices/{deviceId}/status"),
    };

    private static readonly Regex Placeholder = new(@"(\{\w+\})", RegexOptions.Compiled);

    // Resolves a public path to (route, downstream path). Returns null when no
    // route matches - the BFF turns that into a 404.
    public static (ZscRoute Route, string DownstreamPath)? Resolve(string publicPath)
    {
        foreach (var route in All)
        {
            var match = Regex.Match(publicPath, BuildPattern(route.PublicPathTemplate), RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var downstreamPath = Placeholder.Replace(
                route.DownstreamPathTemplate,
                m => Uri.EscapeDataString(match.Groups[NameOf(m.Value)].Value));

            return (route, downstreamPath);
        }

        return null;
    }

    // "/api/v1/devices/{deviceId}/status" -> "^/api/v1/devices/(?<deviceId>[^/]+)/status$".
    // Literal segments are escaped, placeholders become named single-segment groups.
    private static string BuildPattern(string template)
    {
        var pattern = new StringBuilder("^");

        foreach (var part in Placeholder.Split(template))
        {
            if (part.Length == 0)
            {
                continue;
            }

            pattern.Append(IsPlaceholder(part)
                ? $"(?<{NameOf(part)}>[^/]+)"
                : Regex.Escape(part));
        }

        return pattern.Append('$').ToString();
    }

    private static bool IsPlaceholder(string part) => part.Length > 2 && part[0] == '{' && part[^1] == '}';

    private static string NameOf(string placeholder) => placeholder[1..^1];
}
