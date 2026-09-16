using Microsoft.Extensions.Configuration;
using Zsc.CommonRoutes;

namespace Zsc.CommonRoutes.Tests;

public class SubscriptionKeyValidatorTests
{
    private const string DemoKey = "zsc-demo-subscription-key-001";
    private const string WrongKey = "wrong-key-000";

    private static SubscriptionKeyValidator ValidatorWithDemoKey() => new(new[] { DemoKey });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_header_is_reported_as_Missing(string? headerValue)
    {
        Assert.Equal(SubscriptionKeyValidation.Missing, ValidatorWithDemoKey().Validate(headerValue));
    }

    [Fact]
    public void An_unprovisioned_key_is_reported_as_Invalid()
    {
        Assert.Equal(SubscriptionKeyValidation.Invalid, ValidatorWithDemoKey().Validate(WrongKey));
    }

    [Fact]
    public void The_provisioned_demo_key_is_reported_as_Valid()
    {
        Assert.Equal(SubscriptionKeyValidation.Valid, ValidatorWithDemoKey().Validate(DemoKey));
    }

    [Fact]
    public void Validation_is_case_sensitive()
    {
        Assert.Equal(SubscriptionKeyValidation.Invalid, ValidatorWithDemoKey().Validate(DemoKey.ToUpperInvariant()));
    }

    [Fact]
    public void Keys_are_read_from_the_Zsc_SubscriptionKeys_configuration_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zsc:SubscriptionKeys:0"] = DemoKey,
            })
            .Build();

        var validator = new SubscriptionKeyValidator(configuration);

        Assert.Equal(SubscriptionKeyValidation.Valid, validator.Validate(DemoKey));
        Assert.Equal(SubscriptionKeyValidation.Invalid, validator.Validate(WrongKey));
    }
}
