using Shouldly;
using Xunit;
using Zahy.Identity;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerTypeScopePolicyTests
{
    [Theory]
    [InlineData(PartnerType.Aggregator, new[] { ZahyScopes.OrdersRead, ZahyScopes.WebhooksManage })]
    [InlineData(PartnerType.ThreePL, new[] { ZahyScopes.OrdersRead, ZahyScopes.InventoryWrite })]
    [InlineData(PartnerType.Carrier, new[] { ZahyScopes.OrdersRead })]
    [InlineData(PartnerType.Service, new[] { ZahyScopes.CatalogRead, ZahyScopes.OrdersRead })]
    [InlineData(PartnerType.Marketplace, new[] { ZahyScopes.OrdersRead, ZahyScopes.CatalogRead })]
    public void Should_Map_Conservative_Default_Scopes(PartnerType type, string[] expected)
    {
        PartnerTypeScopePolicy.GetDefaultScopes(type).ShouldBe(expected);
    }
}
