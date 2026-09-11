using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class SubscriptionKeyValidatorTests
{
    private const string DemoKey = "zsc-demo-subscription-key-001";
    private const string WrongKey = "wrong-key-000";

    private static SubscriptionKeyValidator Validator() => new(new[] { DemoKey });

    [Fact]
    public void Missing_header_value_is_Missing()
    {
        Assert.Equal(SubscriptionKeyOutcome.Missing, Validator().Validate(null));
    }

    [Fact]
    public void Empty_header_value_is_Missing()
    {
        Assert.Equal(SubscriptionKeyOutcome.Missing, Validator().Validate(string.Empty));
    }

    [Fact]
    public void The_provisioned_demo_key_is_Valid()
    {
        Assert.Equal(SubscriptionKeyOutcome.Valid, Validator().Validate(DemoKey));
    }

    [Fact]
    public void An_unprovisioned_key_is_Invalid()
    {
        Assert.Equal(SubscriptionKeyOutcome.Invalid, Validator().Validate(WrongKey));
    }

    [Fact]
    public void The_wrong_demo_key_from_the_acceptance_brief_is_Invalid()
    {
        Assert.Equal(SubscriptionKeyOutcome.Invalid, Validator().Validate("wrong-key-000"));
    }

    // Comparison is exact/ordinal: a subscription key is an opaque token, not
    // a case-insensitive identifier.
    [Fact]
    public void Comparison_is_case_sensitive()
    {
        var upper = DemoKey.ToUpperInvariant();

        Assert.NotEqual(DemoKey, upper);
        Assert.Equal(SubscriptionKeyOutcome.Invalid, Validator().Validate(upper));
    }

    [Fact]
    public void No_provisioned_keys_means_every_value_is_Invalid()
    {
        var validator = new SubscriptionKeyValidator(Array.Empty<string>());

        Assert.Equal(SubscriptionKeyOutcome.Invalid, validator.Validate(DemoKey));
    }
}
