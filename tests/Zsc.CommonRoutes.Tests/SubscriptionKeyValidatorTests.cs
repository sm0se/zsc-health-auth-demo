using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class SubscriptionKeyValidatorTests
{
    private static readonly SubscriptionKeyValidator Validator =
        new(new HashSet<string>(StringComparer.Ordinal) { "zsc-demo-subscription-key-001" });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_header_value_is_Missing(string? headerValue)
    {
        Assert.Equal(SubscriptionKeyValidationResult.Missing, Validator.Validate(headerValue));
    }

    [Fact]
    public void The_provisioned_demo_key_is_Valid()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Valid, Validator.Validate("zsc-demo-subscription-key-001"));
    }

    [Fact]
    public void An_unprovisioned_key_is_Invalid()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Invalid, Validator.Validate("wrong-key-000"));
    }

    [Fact]
    public void Comparison_is_case_sensitive()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Invalid, Validator.Validate("ZSC-DEMO-SUBSCRIPTION-KEY-001"));
    }
}
