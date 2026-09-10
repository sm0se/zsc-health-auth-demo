using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

// Unit tests for subscription key authentication.
//
// These tests verify that the SubscriptionKeyAuthenticationHandler correctly
// validates subscription keys from the Ocp-Apim-Subscription-Key header.
public class SubscriptionKeyAuthenticationTests
{
    private readonly SubscriptionKeyAuthenticationHandler _handler;
    private readonly DefaultHttpContext _httpContext;

    public SubscriptionKeyAuthenticationTests()
    {
        var options = new Microsoft.Extensions.Options.OptionsMonitor<SubscriptionKeyOptions>(
            new SingletonOptionsMonitor(new SubscriptionKeyOptions
            {
                ValidKeys = new HashSet<string> { "valid-key-001", "valid-key-002" }
            }));

        var loggerFactory = new NullLoggerFactory();
        var urlEncoder = System.Text.Encodings.Web.UrlEncoder.Default;

        _handler = new SubscriptionKeyAuthenticationHandler(options, loggerFactory, urlEncoder);
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task Returns_success_when_a_valid_subscription_key_is_provided()
    {
        _httpContext.Request.Headers[ZscHeaders.SubscriptionKey] = "valid-key-001";

        _handler.Context = new AuthenticationHandlerContext(new AuthenticationScheme(
            SubscriptionKeyAuthenticationHandler.Scheme, null, typeof(SubscriptionKeyAuthenticationHandler)), _httpContext);

        var result = await _handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.Contains(result.Principal!.Claims, c =>
            c.Type == ClaimTypes.NameIdentifier && c.Value == "subscription-key-holder");
    }

    [Fact]
    public async Task Returns_failure_when_an_invalid_subscription_key_is_provided()
    {
        _httpContext.Request.Headers[ZscHeaders.SubscriptionKey] = "invalid-key";

        _handler.Context = new AuthenticationHandlerContext(new AuthenticationScheme(
            SubscriptionKeyAuthenticationHandler.Scheme, null, typeof(SubscriptionKeyAuthenticationHandler)), _httpContext);

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Returns_no_result_when_subscription_key_header_is_missing()
    {
        // Don't add the header at all

        _handler.Context = new AuthenticationHandlerContext(new AuthenticationScheme(
            SubscriptionKeyAuthenticationHandler.Scheme, null, typeof(SubscriptionKeyAuthenticationHandler)), _httpContext);

        var result = await _handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.False(result.Failure == null); // NoResult means no error message
        // NoResult is characterized by both Succeeded=false and no error message
        Assert.Null(result.Principal);
    }

    private class SingletonOptionsMonitor : Microsoft.Extensions.Options.IOptionsMonitor<SubscriptionKeyOptions>
    {
        private readonly SubscriptionKeyOptions _options;

        public SingletonOptionsMonitor(SubscriptionKeyOptions options)
        {
            _options = options;
        }

        public SubscriptionKeyOptions CurrentValue => _options;
        public SubscriptionKeyOptions Get(string name) => _options;
        public IDisposable OnChange(Action<SubscriptionKeyOptions, string> listener) => new NoOp();

        private class NoOp : IDisposable
        {
            public void Dispose() { }
        }
    }
}
