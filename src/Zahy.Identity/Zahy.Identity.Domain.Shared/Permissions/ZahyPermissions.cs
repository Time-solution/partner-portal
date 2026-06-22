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

        /// <summary>Service partners — author own catalog items (tiers) only.</summary>
        public const string AuthorSelf = Default + ".Author.Self";

        /// <summary>Platform admin — author any partner catalog including managed purchase agreements.</summary>
        public const string AuthorManaged = Default + ".Author.Managed";
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

        /// <summary>
        /// Read ANY partner/merchant finance ledger (the cross-entity "see all" capability). Granted to
        /// the accountant (Platform.Finance) and Platform.SuperAdmin ONLY — deliberately NOT implied by
        /// <see cref="Partners.Manage"/>, so partner-ops cannot read arbitrary merchant/partner ledgers.
        /// Partner/merchant principals are scoped to their OWN entity by context, not by this permission.
        /// </summary>
        public const string ReadAll = Default + ".ReadAll";

        /// <summary>
        /// Author an ad-hoc (manual) invoice with custom recipient + line items. Granted to the accountant
        /// (Platform.Finance) and Platform.SuperAdmin ONLY — never to partner/merchant roles (partner-side
        /// Finance stays read-only). Deliberately NOT implied by <see cref="ReadAll"/>: reading every ledger
        /// must not confer the right to mint invoices.
        /// </summary>
        public const string WriteManualInvoice = Default + ".WriteManualInvoice";
    }

    public static class Commission
    {
        public const string Default = GroupName + ".Commission";

        /// <summary>Approve / reverse / mark-paid on commission ledger entries (platform finance).</summary>
        public const string Approve = Default + ".Approve";
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

        /// <summary>Manage the bank registry (accountant). Add/edit/deactivate banks + their ledger accounts.
        /// Granted to Platform.Finance (accountant) and Platform.SuperAdmin only — never to partner/merchant.</summary>
        public const string BankRegistryManage = Default + ".BankRegistry.Manage";
    }

    /// <summary>Platform-wide administration (super power).</summary>
    public const string Admin = GroupName + ".Admin";

    public static string[] All()
    {
        return new[]
        {
            Catalog.Read,
            Catalog.Write,
            Catalog.AuthorSelf,
            Catalog.AuthorManaged,
            Orders.Read,
            Inventory.Write,
            Webhooks.Manage,
            Payouts.Read,
            Partners.Manage,
            Roles.Manage,
            Finance.KycReview,
            Finance.ReadAll,
            Finance.WriteManualInvoice,
            Commission.Approve,
            Settlement.Read,
            Settlement.Reconcile,
            Settlement.Disburse,
            Settlement.BankRegistryManage,
            Admin
        };
    }
}
