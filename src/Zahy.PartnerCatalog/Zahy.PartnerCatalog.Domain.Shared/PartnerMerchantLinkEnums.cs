namespace Zahy.PartnerCatalog;

/// <summary>
/// R1 — lifecycle of a partner&lt;-&gt;merchant participation LINK: the data-sharing RELATIONSHIP governed by
/// a partner GRANT (offer of scope) and a merchant CONSENT (PDPL). This is the relationship-level envelope
/// that the per-item <see cref="MerchantActivation"/> and per-package <see cref="UsagePackageSelection"/>
/// sit inside — it does NOT duplicate them (those stay the source of truth for catalog-item / package
/// participation). DOMAIN MODEL ONLY (spike): no persistence, no app service, no wiring.
/// </summary>
public enum PartnerMerchantLinkState
{
    /// <summary>Created, no grant offered yet.</summary>
    Draft = 0,

    /// <summary>Partner has offered a grant; merchant has not consented to the current version.</summary>
    GrantOffered = 1,

    /// <summary>A grant exists and a consent was once given, but it no longer matches the current grant version.</summary>
    ConsentPending = 2,

    /// <summary>Live grant + consent matching the current grant version; neither side revoked.</summary>
    Active = 3,

    /// <summary>Temporarily paused; grant/consent may still be valid but the link is not active.</summary>
    Suspended = 4,

    /// <summary>Partner revoked the grant. Terminal.</summary>
    Revoked = 5,

    /// <summary>Merchant-side termination of the relationship. Terminal (reserved for a later slice).</summary>
    Terminated = 6,
}

/// <summary>State of the partner's GRANT (the offer of <see cref="GrantedScope"/> to the merchant).</summary>
public enum GrantState
{
    None = 0,
    Offered = 1,
    Revoked = 2,
}

/// <summary>State of the merchant's CONSENT to a specific grant version (PDPL data-sharing consent).</summary>
public enum ConsentState
{
    None = 0,
    Given = 1,
    Withdrawn = 2,
}

/// <summary>
/// Why a link reached the terminal <see cref="PartnerMerchantLinkState.Terminated"/> state. This is a
/// LIFECYCLE end (the relationship ran its course) — distinct from <see cref="GrantState.Revoked"/>, which
/// is a deliberate PARTY decision to pull the grant.
/// </summary>
public enum LinkTerminationReason
{
    /// <summary>The grant's validity window lapsed (ValidUntil passed) and the link was wound down.</summary>
    ValidityLapsed = 1,

    /// <summary>The partner or merchant account was closed.</summary>
    AccountClosure = 2,

    /// <summary>The merchant was offboarded from the partner.</summary>
    Offboard = 3,
}
