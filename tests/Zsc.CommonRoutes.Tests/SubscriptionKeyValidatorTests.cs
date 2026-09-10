using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class SubscriptionKeyValidatorTests
{
    [Fact]
    public void Returns_Missing_when_header_value_is_null()
    {
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string> { "valid-key" }
        };
        var validator = new SubscriptionKeyValidator(options);

        var result = validator.Validate(null);

        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Missing, result);
    }

    [Fact]
    public void Returns_Missing_when_header_value_is_empty()
    {
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string> { "valid-key" }
        };
        var validator = new SubscriptionKeyValidator(options);

        var result = validator.Validate("");

        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Missing, result);
    }

    [Fact]
    public void Returns_Valid_when_key_is_in_valid_set()
    {
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string> { "valid-key", "another-key" }
        };
        var validator = new SubscriptionKeyValidator(options);

        var result = validator.Validate("valid-key");

        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Valid, result);
    }

    [Fact]
    public void Returns_Invalid_when_key_is_not_in_valid_set()
    {
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string> { "valid-key" }
        };
        var validator = new SubscriptionKeyValidator(options);

        var result = validator.Validate("invalid-key");

        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Invalid, result);
    }

    [Fact]
    public void Validation_is_case_sensitive()
    {
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string> { "MyKey" }
        };
        var validator = new SubscriptionKeyValidator(options);

        var resultExact = validator.Validate("MyKey");
        var resultLower = validator.Validate("mykey");
        var resultUpper = validator.Validate("MYKEY");

        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Valid, resultExact);
        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Invalid, resultLower);
        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Invalid, resultUpper);
    }

    [Fact]
    public void Returns_Valid_with_demo_subscription_key()
    {
        var demoKey = "zsc-demo-subscription-key-001";
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string> { demoKey }
        };
        var validator = new SubscriptionKeyValidator(options);

        var result = validator.Validate(demoKey);

        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Valid, result);
    }

    [Fact]
    public void Returns_Invalid_with_wrong_key()
    {
        var options = new SubscriptionKeyAuthenticationOptions
        {
            ValidKeys = new HashSet<string> { "zsc-demo-subscription-key-001" }
        };
        var validator = new SubscriptionKeyValidator(options);

        var result = validator.Validate("wrong-key-000");

        Assert.Equal(SubscriptionKeyValidator.ValidationResult.Invalid, result);
    }
}
