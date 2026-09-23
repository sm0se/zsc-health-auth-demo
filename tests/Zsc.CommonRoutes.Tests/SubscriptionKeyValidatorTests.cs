using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class SubscriptionKeyValidatorTests
{
    private static readonly SubscriptionKeyValidator Validator = new(new[] { "zsc-demo-subscription-key-001" });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_value_returns_missing(string? value)
    {
        Assert.Equal(SubscriptionKeyValidationResult.Missing, Validator.Validate(value));
    }

    [Theory]
    [InlineData("wrong-key-000")]
    [InlineData("ZSC-DEMO-SUBSCRIPTION-KEY-001")]
    [InlineData("zsc-demo-subscription-key-001 ")]
    public void Unconfigured_or_case_changed_value_returns_invalid(string value)
    {
        Assert.Equal(SubscriptionKeyValidationResult.Invalid, Validator.Validate(value));
    }

    [Fact]
    public void Configured_demo_key_returns_valid()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Valid, Validator.Validate("zsc-demo-subscription-key-001"));
    }
}
