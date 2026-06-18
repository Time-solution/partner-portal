namespace Zahy.Identity;

/// <summary>
/// OAuth/OIDC resource scopes for the Zahy Partner Platform.
/// Each scope maps to an API area and (in Phase 1 Step 3) to an ABP permission group.
/// </summary>
public static class ZahyScopes
{
    /// <summary>Logical API resource name (token audience).</summary>
    public const string ApiResource = "Zahy";

    public const string CatalogRead = "catalog:read";
    public const string CatalogWrite = "catalog:write";
    public const string OrdersRead = "orders:read";
    public const string InventoryWrite = "inventory:write";
    public const string WebhooksManage = "webhooks:manage";
    public const string PayoutsRead = "payouts:read";
    public const string PartnersManage = "partners:manage";
    public const string Admin = "admin";

    public static readonly (string Name, string DisplayName)[] All =
    {
        (CatalogRead, "Read catalog"),
        (CatalogWrite, "Write catalog"),
        (OrdersRead, "Read orders"),
        (InventoryWrite, "Write inventory"),
        (WebhooksManage, "Manage webhooks"),
        (PayoutsRead, "Read payouts"),
        (PartnersManage, "Manage partners"),
        (Admin, "Administer platform"),
    };

    public static string[] Names()
    {
        var names = new string[All.Length];
        for (var i = 0; i < All.Length; i++)
        {
            names[i] = All[i].Name;
        }
        return names;
    }
}
