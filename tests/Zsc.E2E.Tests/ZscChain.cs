using System.Net.Http.Json;
using System.Text.Json;

namespace Zsc.E2E.Tests;

// Talks to the running chain over real HTTP, entering it at the gateway exactly
// as an external caller would.
public sealed class ZscChain
{
    public static readonly string GatewayUrl =
        Environment.GetEnvironmentVariable("ZSC_GATEWAY") ?? "http://127.0.0.1:5080";

    // The subscription key the demo provisions for its one consumer. The value
    // has to be configured into whichever service terminates subscription-key
    // authentication - see docs/REQUIREMENT-R1.md.
    public static readonly string SubscriptionKey =
        Environment.GetEnvironmentVariable("ZSC_SUBSCRIPTION_KEY") ?? "zsc-demo-subscription-key-001";

    public const string SubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";

    private static readonly HttpClient Client = new() { BaseAddress = new Uri(GatewayUrl), Timeout = TimeSpan.FromSeconds(30) };
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _token;

    public static async Task<string> BearerTokenAsync()
    {
        if (_token is not null)
        {
            return _token;
        }

        await TokenLock.WaitAsync();
        try
        {
            if (_token is null)
            {
                using var response = await Client.PostAsync("/dev/token", content: null);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
                _token = payload.GetProperty("access_token").GetString()
                         ?? throw new InvalidOperationException("The dev token endpoint returned no access_token.");
            }
        }
        finally
        {
            TokenLock.Release();
        }

        return _token;
    }

    public static Task<HttpResponseMessage> GetAsync(string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return Client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> GetWithBearerAsync(string path) =>
        await GetAsync(path, ("Authorization", $"Bearer {await BearerTokenAsync()}"));

    public static Task<HttpResponseMessage> GetWithSubscriptionKeyAsync(string path, string? key = null) =>
        GetAsync(path, (SubscriptionKeyHeader, key ?? SubscriptionKey));
}
