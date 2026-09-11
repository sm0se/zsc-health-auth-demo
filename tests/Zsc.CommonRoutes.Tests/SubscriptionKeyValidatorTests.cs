using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class SubscriptionKeyValidatorTests
{
    private static readonly SubscriptionKeyValidator Validator =
        SubscriptionKeyValidator.ForKeys(new[] { "zsc-demo-subscription-key-001" });

    [Fact]
    public void Missing_header_value_is_reported_as_missing()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Missing, Validator.Validate(null));
        Assert.Equal(SubscriptionKeyValidationResult.Missing, Validator.Validate(string.Empty));
    }

    [Fact]
    public void The_demo_key_validates()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Valid, Validator.Validate("zsc-demo-subscription-key-001"));
    }

    [Fact]
    public void The_documented_wrong_key_is_invalid()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Invalid, Validator.Validate("wrong-key-000"));
    }

    [Fact]
    public void An_unprovisioned_key_is_invalid()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Invalid, Validator.Validate("not-a-provisioned-key"));
    }

    // A subscription key is an opaque provisioned secret, not a case-insensitive
    // identifier - a caller cannot upper-case their way into someone else's key.
    [Fact]
    public void Comparison_is_case_sensitive()
    {
        Assert.Equal(SubscriptionKeyValidationResult.Invalid, Validator.Validate("ZSC-DEMO-SUBSCRIPTION-KEY-001"));
    }
}
