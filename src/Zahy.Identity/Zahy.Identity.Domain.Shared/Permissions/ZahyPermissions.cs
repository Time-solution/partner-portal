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

    public static class Finance
    {
        public const string Default = GroupName + ".Finance";
        public const string KycReview = Default + ".KycReview";
    }

    public static class Settlement
    {
        public const string Default = GroupName + ".Settlement";

        /// <summary>Read settlement books/cases/journals (scoped to own partner+book for partner roles).</summary>
        public const string Read = Default + ".Read";

        /// <summary>Reconcile settlement (accountant). Does NOT permit moving money.</summary>
        public const string Reconcile = Default + ".Reconcile";

        /// <summary>Disburse funds. Platform admin only; never granted to accountant or partner roles.</summary>
        public const string Disburse = Default + ".Disburse";
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
            Finance.KycReview,
            Settlement.Read,
            Settlement.Reconcile,
            Settlement.Disburse,
            Admin
        };
    }
}
