namespace Zahy.Identity.Roles;

/// <summary>The audience a role belongs to.</summary>
public enum ZahyRoleScope
{
    /// <summary>Host-level platform operators.</summary>
    Platform,

    /// <summary>Tenant-level (each merchant is an ABP tenant).</summary>
    Merchant,

    /// <summary>Host-level partner aggregates (isolated by partner_id claim).</summary>
    Partner
}

/// <summary>Well-known role names, grouped by scope.</summary>
public static class ZahyRoles
{
    // Platform (host)
    public const string PlatformSuperAdmin = "Platform.SuperAdmin";
    public const string PlatformPartnerOps = "Platform.PartnerOps";
    public const string PlatformFinance = "Platform.Finance";
    public const string PlatformSupport = "Platform.Support";
    public const string PlatformReadOnly = "Platform.ReadOnly";

    // Merchant (tenant)
    public const string MerchantOwner = "Merchant.Owner";
    public const string MerchantManager = "Merchant.Manager";
    public const string MerchantStaff = "Merchant.Staff";
    public const string MerchantViewer = "Merchant.Viewer";

    // Partner (host aggregate)
    public const string PartnerOwner = "Partner.Owner";
    public const string PartnerManager = "Partner.Manager";
    public const string PartnerStaff = "Partner.Staff";
}
