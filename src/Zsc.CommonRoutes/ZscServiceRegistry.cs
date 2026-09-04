using Microsoft.Extensions.Configuration;

namespace Zsc.CommonRoutes;

// Service name -> base address, read from the `Zsc:Services` configuration
// section. Each service declares only the neighbours it actually calls.
public sealed class ZscServiceRegistry
{
    public const string SectionName = "Zsc:Services";

    private readonly IReadOnlyDictionary<string, string> _addresses;

    public ZscServiceRegistry(IConfiguration configuration)
    {
        _addresses = configuration.GetSection(SectionName)
            .GetChildren()
            .Where(child => !string.IsNullOrWhiteSpace(child.Value))
            .ToDictionary(child => child.Key, child => child.Value!.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);
    }

    public string Resolve(string serviceName)
    {
        if (!_addresses.TryGetValue(serviceName, out var address))
        {
            throw new KeyNotFoundException(
                $"No address configured for service '{serviceName}'. Add it under '{SectionName}'.");
        }

        return address;
    }
}
