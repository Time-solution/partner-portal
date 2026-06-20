using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

public class SettlementFlowProfileTests
{
    private static SettlementFlowProfileResolver FullResolver() =>
        new(new ISettlementFlowProfile[] { new AggregatorFlowProfile(), new ServiceFlowProfile() });

    [Fact]
    public void Resolver_Returns_The_Profile_For_Each_Book()
    {
        var resolver = FullResolver();
        resolver.Resolve(SettlementBook.Marketplace).ShouldBeOfType<AggregatorFlowProfile>();
        resolver.Resolve(SettlementBook.Integration).ShouldBeOfType<ServiceFlowProfile>();
    }

    [Fact]
    public void Resolver_Throws_For_An_Unmapped_Book()
    {
        var partial = new SettlementFlowProfileResolver(new ISettlementFlowProfile[] { new AggregatorFlowProfile() });

        Should.Throw<BusinessException>(() => partial.Resolve(SettlementBook.Integration))
            .Code.ShouldBe(SettlementCaseErrorCodes.UnknownFlowProfile);
    }

    [Fact]
    public void Aggregator_And_Service_Account_Trees_Are_Isolated()
    {
        var aggregator = new AggregatorFlowProfile();
        var service = new ServiceFlowProfile();

        // Aggregator owns the marketplace-only accounts, not the service ones.
        aggregator.Owns(SettlementAccountType.AggregatorClearing).ShouldBeTrue();
        aggregator.Owns(SettlementAccountType.MerchantPayable).ShouldBeTrue();
        aggregator.Owns(SettlementAccountType.PartnerPayable).ShouldBeFalse();
        aggregator.Owns(SettlementAccountType.ShippingMarginRevenue).ShouldBeTrue();

        // Service owns the integration-only accounts, not the marketplace ones.
        service.Owns(SettlementAccountType.PartnerPayable).ShouldBeTrue();
        service.Owns(SettlementAccountType.ShippingMarginRevenue).ShouldBeTrue();
        service.Owns(SettlementAccountType.AggregatorClearing).ShouldBeFalse();
        service.Owns(SettlementAccountType.MerchantPayable).ShouldBeFalse();
    }
}
