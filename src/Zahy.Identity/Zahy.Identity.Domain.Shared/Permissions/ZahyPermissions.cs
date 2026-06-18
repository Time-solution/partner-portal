namespace Zahy.Identity.Permissions;

/// <summary>
/// Permission names for the Zahy Partner Platform. Grouped to mirror the OAuth
/// resource scopes (catalog/orders/inventory/webhooks/payouts/partners/admin).
/// </summary>
public static class ZahyPermissions
{
    public const string GroupName = "Zahy";

    public static class Catalog
    {
        public const string Default = GroupName + ".Catalog";
        public const string Read = Default + ".Read";
        public const string Write = Default + ".Write";
    }

    public static class Orders
    {
        public const string Default = GroupName + ".Orders";
        public const string Read = Default + ".Read";
    }

    public static class Inventory
    {
        public const string Default = GroupName + ".Inventory";
        public const string Write = Default + ".Write";
    }

    public static class Webhooks
    {
        public const string Default = GroupName + ".Webhooks";
        public const string Manage = Default + ".Manage";
    }

    public static class Payouts
    {
        public const string Default = GroupName + ".Payouts";
        public const string Read = Default + ".Read";
    }

    public static class Partners
    {
        public const string Default = GroupName + ".Partners";
        public const string Manage = Default + ".Manage";
    }

    public static class Roles
    {
        public const string Default = GroupName + ".Roles";
        public const string Manage = Default + ".Manage";
    }

    /// <summary>Platform-wide administration (super power).</summary>
    public const string Admin = GroupName + ".Admin";

    public static string[] All()
    {
        return new[]
        {
            Catalog.Read,
            Catalog.Write,
            Orders.Read,
            Inventory.Write,
            Webhooks.Manage,
            Payouts.Read,
            Partners.Manage,
            Roles.Manage,
            Admin
        };
    }
}
