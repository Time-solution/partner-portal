using System.Collections.Generic;
using Zahy.Identity;

namespace Zahy.PartnerPlatform.Partners;

/// <summary>Conservative default OAuth scopes per partner connector type (PON-4).</summary>
public static class PartnerTypeScopePolicy
{
    public static IReadOnlyList<string> GetDefaultScopes(PartnerType type) =>
        type switch
        {
            PartnerType.Aggregator => [ZahyScopes.OrdersRead, ZahyScopes.WebhooksManage],
            PartnerType.ThreePL => [ZahyScopes.OrdersRead, ZahyScopes.InventoryWrite],
            PartnerType.Carrier => [ZahyScopes.OrdersRead],
            PartnerType.Service => [ZahyScopes.CatalogRead, ZahyScopes.OrdersRead],
            PartnerType.Marketplace => [ZahyScopes.OrdersRead, ZahyScopes.CatalogRead],
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown partner type.")
        };
}
