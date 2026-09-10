using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class SubscriptionKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task Returns_NoResult_when_subscription_key_header_is_missing()
    {
        var handler = CreateHandler(new[] { "valid-key" });
        var context = CreateHttpContext();

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.False(result.Failure != null);  // NoResult means both are false
    }

    [Fact]
    public async Task Succeeds_when_subscription_key_is_valid()
    {
        var handler = CreateHandler(new[] { "valid-key" });
        var context = CreateHttpContext(subscriptionKey: "valid-key");

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.Equal(SubscriptionKeyAuthenticationOptions.Scheme, result.Principal!.Identity!.AuthenticationType);
    }

    [Fact]
    public async Task Fails_when_subscription_key_is_invalid()
    {
        var handler = CreateHandler(new[] { "valid-key" });
        var context = CreateHttpContext(subscriptionKey: "invalid-key");

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task Fails_when_subscription_key_is_empty()
    {
        var handler = CreateHandler(new[] { "valid-key" });
        var context = CreateHttpContext(subscriptionKey: "");

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Succeeds_with_any_of_multiple_configured_keys()
    {
        var handler = CreateHandler(new[] { "key-1", "key-2", "key-3" });

        // Test with each key
        foreach (var key in new[] { "key-1", "key-2", "key-3" })
        {
            var context = CreateHttpContext(subscriptionKey: key);
            var result = await handler.AuthenticateAsync();

            Assert.True(result.Succeeded);
        }
    }

    private SubscriptionKeyAuthenticationHandler CreateHandler(string[] validKeys)
    {
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string>(validKeys, StringComparer.Ordinal)
        };

        var loggerFactory = new LoggerFactory();

        return new SubscriptionKeyAuthenticationHandler(
            OptionsMonitorFake.Create(options),
            loggerFactory,
            UrlEncoder.Default);
    }

    private HttpContext CreateHttpContext(string? subscriptionKey = null)
    {
        var context = new DefaultHttpContext();
        if (subscriptionKey is not null)
        {
            context.Request.Headers[SubscriptionKeyAuthenticationOptions.HeaderName] = subscriptionKey;
        }

        return context;
    }

    private sealed class OptionsMonitorFake : IOptionsMonitor<SubscriptionKeyAuthenticationOptions>
    {
        private readonly SubscriptionKeyAuthenticationOptions _options;

        private OptionsMonitorFake(SubscriptionKeyAuthenticationOptions options) => _options = options;

        public static OptionsMonitorFake Create(SubscriptionKeyAuthenticationOptions options) => new(options);

        public SubscriptionKeyAuthenticationOptions CurrentValue => _options;

        public SubscriptionKeyAuthenticationOptions Get(string? name) => _options;

        public IDisposable? OnChange(Action<SubscriptionKeyAuthenticationOptions, string?> listener) => null;
    }
}
