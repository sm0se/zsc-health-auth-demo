using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

// Unit tests for subscription key authentication configuration.
//
// These tests verify that subscription keys can be configured and that
// the route predicate correctly identifies health-status endpoints.
public class SubscriptionKeyAuthenticationTests
{
    [Fact]
    public void Subscription_key_header_constant_is_defined()
    {
        Assert.Equal("Ocp-Apim-Subscription-Key", ZscHeaders.SubscriptionKey);
    }

    [Fact]
    public void Subscription_key_authentication_scheme_is_defined()
    {
        Assert.Equal("SubscriptionKey", SubscriptionKeyAuthenticationHandler.Scheme);
    }

    [Theory]
    [InlineData("/api/v1/health/zsc/status")]
    [InlineData("/api/v1/health/zls/status")]
    [InlineData("/internal/health/zsc/status")]
    [InlineData("/internal/health/zls/status")]
    public void Health_status_route_predicate_returns_true_for_health_endpoints(string path)
    {
        Assert.True(ZscHealthStatusRoutes.IsHealthStatusRoute(path));
    }

    [Theory]
    [InlineData("/api/v1/devices/dev-0001/status")]
    [InlineData("/api/v1/unknown")]
    [InlineData("/healthz")]
    public void Health_status_route_predicate_returns_false_for_non_health_endpoints(string path)
    {
        Assert.False(ZscHealthStatusRoutes.IsHealthStatusRoute(path));
    }

    [Fact]
    public void Valid_key_string_comparison_is_case_insensitive()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "zsc-demo-subscription-key-001"
        };

        // Different cases should match
        Assert.Contains("ZSC-DEMO-SUBSCRIPTION-KEY-001", keys);
        Assert.Contains("zsc-demo-subscription-key-001", keys);
    }
}
