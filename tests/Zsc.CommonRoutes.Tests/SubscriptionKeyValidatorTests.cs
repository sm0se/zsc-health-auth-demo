using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

// Unit tests for the key decision behind Requirement R1's subscription-key
// authentication. The demo key below is only test fixture data - the production
// keys are provisioned per service through the Zsc:SubscriptionKeys
// configuration section and are never hard-coded in Zsc.CommonRoutes.
public class SubscriptionKeyValidatorTests
{
    private const string DemoKey = "zsc-demo-subscription-key-001";

    private static SubscriptionKeyValidator Validator(params string[] provisionedKeys) =>
        new(provisionedKeys);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_or_empty_header_is_missing_not_invalid(string? headerValue)
    {
        var validator = Validator(DemoKey);

        Assert.Equal(SubscriptionKeyResult.Missing, validator.Validate(headerValue));
    }

    [Fact]
    public void The_provisioned_demo_key_is_valid()
    {
        var validator = Validator(DemoKey);

        Assert.Equal(SubscriptionKeyResult.Valid, validator.Validate("zsc-demo-subscription-key-001"));
    }

    [Fact]
    public void A_wrong_key_is_invalid()
    {
        var validator = Validator(DemoKey);

        Assert.Equal(SubscriptionKeyResult.Invalid, validator.Validate("wrong-key-000"));
    }

    [Fact]
    public void A_key_that_looks_provisioned_but_is_not_on_the_list_is_invalid()
    {
        var validator = Validator("zsc-demo-subscription-key-002");

        Assert.Equal(SubscriptionKeyResult.Invalid, validator.Validate(DemoKey));
    }

    // A subscription key is an opaque token: comparison is ordinal, so a
    // case-twisted copy of the demo key must not authenticate.
    [Fact]
    public void Keys_are_case_sensitive()
    {
        var validator = Validator(DemoKey);

        Assert.Equal(SubscriptionKeyResult.Invalid, validator.Validate("ZSC-DEMO-SUBSCRIPTION-KEY-001"));
    }

    [Fact]
    public void A_service_with_no_provisioned_keys_accepts_no_key()
    {
        var validator = Validator();

        Assert.Equal(SubscriptionKeyResult.Invalid, validator.Validate(DemoKey));
    }
}
