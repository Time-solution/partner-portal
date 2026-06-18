using Shouldly;
using Xunit;

namespace Zahy.PartnerPlatform.Partners;

public class SaudiIbanValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Accept_Empty_Iban(string? iban)
    {
        SaudiIbanValidator.IsValidOrEmpty(iban).ShouldBeTrue();
    }

    [Fact]
    public void Should_Accept_Valid_Saudi_Iban()
    {
        SaudiIbanValidator.IsValid("SA0380000000608010167519").ShouldBeTrue();
    }

    [Theory]
    [InlineData("SA038000000060801016751")]
    [InlineData("AE0380000000608010167519")]
    [InlineData("SA038000000060801016751X")]
    public void Should_Reject_Invalid_Iban(string iban)
    {
        SaudiIbanValidator.IsValid(iban).ShouldBeFalse();
    }

    [Fact]
    public void Should_Normalize_Spaces_And_Case()
    {
        SaudiIbanValidator.Normalize("sa 0380 0000 0060 8010 1675 19")
            .ShouldBe("SA0380000000608010167519");
    }
}
