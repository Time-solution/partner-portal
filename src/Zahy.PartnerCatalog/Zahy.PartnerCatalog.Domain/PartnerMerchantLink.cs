using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.PartnerCatalog;

/// <summary>
/// R1 — aggregate root for a partner&lt;-&gt;merchant participation LINK: the data-sharing RELATIONSHIP
/// governed by a partner GRANT (an offer of <see cref="GrantedScope"/>) and a merchant CONSENT (PDPL).
/// This is the relationship-level envelope that per-item <see cref="MerchantActivation"/> and per-package
/// <see cref="UsagePackageSelection"/> sit inside — it does NOT duplicate them.
///
/// <para>Core invariant (PDPL): the link <see cref="IsActive"/> iff a non-revoked grant is present AND the
/// merchant's consent is <see cref="ConsentState.Given"/> AND that consent is pinned to the CURRENT
/// <see cref="GrantVersion"/> AND neither side has revoked/terminated. A consent pinned to a superseded
/// grant version is automatically NOT active.</para>
///
/// <para>DOMAIN MODEL ONLY (spike): no DbContext mapping, no migration, no app service, no wiring.</para>
/// </summary>
public class PartnerMerchantLink : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public Guid MerchantId { get; private set; }

    public PartnerMerchantLinkState State { get; private set; } = PartnerMerchantLinkState.Draft;

    // --- Grant (partner side) ---
    public GrantedScope? GrantedScope { get; private set; }

    /// <summary>Monotonic counter; bumped on every real <see cref="ReviseGrant"/>. First offer = version 1.</summary>
    public int GrantVersion { get; private set; }

    public Guid? GrantedBy { get; private set; }

    public DateTime? GrantedAt { get; private set; }

    public GrantState GrantState { get; private set; } = GrantState.None;

    // --- Consent (merchant side) ---
    public int? ConsentedGrantVersion { get; private set; }

    public Guid? ConsentedBy { get; private set; }

    public DateTime? ConsentedAt { get; private set; }

    public ConsentState ConsentState { get; private set; } = ConsentState.None;

    public Guid? RevokedBy { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public LinkTerminationReason? TerminationReason { get; private set; }

    public DateTime? TerminatedAt { get; private set; }

    protected PartnerMerchantLink()
    {
    }

    public static PartnerMerchantLink Create(Guid id, Guid partnerId, Guid merchantId)
    {
        if (partnerId == Guid.Empty || merchantId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidPartnerMerchantLink)
                .WithData("Reason", "IdsRequired");
        }

        if (partnerId == merchantId)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidPartnerMerchantLink)
                .WithData("Reason", "PartnerAndMerchantMustDiffer");
        }

        return new PartnerMerchantLink
        {
            Id = id,
            PartnerId = partnerId,
            MerchantId = merchantId,
            State = PartnerMerchantLinkState.Draft,
            GrantState = GrantState.None,
            ConsentState = ConsentState.None,
            GrantVersion = 0
        };
    }

    /// <summary>
    /// THE invariant, evaluated against <paramref name="asOfUtc"/>. Active iff a non-revoked grant is present,
    /// the merchant consent is Given and pinned to the current grant version, the link state is Active (so
    /// Suspended / Revoked / Terminated are not), AND the grant's validity window covers <paramref name="asOfUtc"/>.
    ///
    /// <para>EXPIRY POLICY: an expired (or not-yet-valid) grant SUPPRESSES IsActive but does NOT mutate
    /// <see cref="State"/> — the aggregate has no time-based side effects. Recording the terminal state is an
    /// explicit <see cref="Terminate"/> call (e.g. with <see cref="LinkTerminationReason.ValidityLapsed"/>).</para>
    /// </summary>
    public bool IsActiveAsOf(DateTime asOfUtc) =>
        SatisfiesActiveInvariant &&
        State == PartnerMerchantLinkState.Active &&
        IsWithinValidity(asOfUtc);

    /// <summary>Convenience overload evaluated against the current UTC clock (pure query; no mutation).</summary>
    public bool IsActive => IsActiveAsOf(DateTime.UtcNow);

    /// <summary>True when the grant's <see cref="GrantedScope.ValidUntil"/> is in the past as of <paramref name="asOfUtc"/>.</summary>
    public bool IsExpiredAsOf(DateTime asOfUtc) =>
        GrantedScope?.ValidUntil is DateTime validUntil && asOfUtc > validUntil;

    /// <summary>Partner offers the FIRST grant (Draft -&gt; GrantOffered, version 1).</summary>
    public void OfferGrant(GrantedScope scope, Guid grantedBy, DateTime atUtc)
    {
        Check.NotNull(scope, nameof(scope));
        EnsureNotTerminal();

        if (State != PartnerMerchantLinkState.Draft)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidLinkStateTransition)
                .WithData("From", State.ToString())
                .WithData("Action", nameof(OfferGrant));
        }

        GrantVersion = 1;
        GrantedScope = scope;
        GrantedBy = grantedBy;
        GrantedAt = atUtc;
        GrantState = GrantState.Offered;
        State = PartnerMerchantLinkState.GrantOffered;
    }

    /// <summary>
    /// Partner revises the grant. A real content change bumps <see cref="GrantVersion"/> and, because any
    /// existing consent is now pinned to a superseded version, drops an Active link to ConsentPending.
    /// Revising with IDENTICAL scope content (same hash) is a no-op — no version bump, no state change.
    /// </summary>
    public void ReviseGrant(GrantedScope scope, Guid revisedBy, DateTime atUtc)
    {
        Check.NotNull(scope, nameof(scope));
        EnsureNotTerminal();

        if (GrantState != GrantState.Offered || GrantedScope is null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.GrantNotOffered)
                .WithData("Action", nameof(ReviseGrant));
        }

        if (GrantedScope.Hash == scope.Hash)
        {
            return; // no content change → idempotent no-op
        }

        GrantVersion++;
        GrantedScope = scope;
        GrantedBy = revisedBy;
        GrantedAt = atUtc;

        // The consent (if any) now points at an older version → the link can no longer be Active.
        State = ConsentState == ConsentState.Given
            ? PartnerMerchantLinkState.ConsentPending
            : PartnerMerchantLinkState.GrantOffered;
    }

    /// <summary>Merchant consents to a specific (current) grant version. Re-consenting the same version is a no-op.</summary>
    public void GiveConsent(int grantVersion, Guid consentedBy, DateTime atUtc)
    {
        EnsureNotTerminal();

        if (GrantState != GrantState.Offered || GrantedScope is null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.GrantNotOffered)
                .WithData("Action", nameof(GiveConsent));
        }

        if (grantVersion != GrantVersion)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.StaleConsentVersion)
                .WithData("ExpectedVersion", GrantVersion)
                .WithData("ProvidedVersion", grantVersion);
        }

        if (ConsentState == ConsentState.Given && ConsentedGrantVersion == grantVersion)
        {
            return; // idempotent
        }

        ApplyConsent(grantVersion, consentedBy, atUtc);
    }

    /// <summary>
    /// Standing-grant special case: for a PUBLIC grant, a merchant ACTIVATION stands in as the consent act
    /// (consent to the current grant version). Rejected for a private (non-standing) grant.
    /// </summary>
    public void AcceptByActivation(Guid consentedBy, DateTime atUtc)
    {
        EnsureNotTerminal();

        if (GrantState != GrantState.Offered || GrantedScope is null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.GrantNotOffered)
                .WithData("Action", nameof(AcceptByActivation));
        }

        if (!GrantedScope.IsStandingGrant)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.StandingGrantRequired)
                .WithData("Action", nameof(AcceptByActivation));
        }

        if (ConsentState == ConsentState.Given && ConsentedGrantVersion == GrantVersion)
        {
            return; // idempotent
        }

        ApplyConsent(GrantVersion, consentedBy, atUtc);
    }

    /// <summary>Merchant withdraws consent. The link leaves Active; the grant stays offered awaiting re-consent.</summary>
    public void WithdrawConsent(DateTime atUtc)
    {
        EnsureNotTerminal();

        if (ConsentState != ConsentState.Given)
        {
            return; // nothing to withdraw → idempotent
        }

        ConsentState = ConsentState.Withdrawn;
        ConsentedGrantVersion = null;
        ConsentedBy = null;
        ConsentedAt = null;
        State = PartnerMerchantLinkState.ConsentPending;
    }

    public void Suspend(DateTime atUtc)
    {
        EnsureNotTerminal();

        if (State != PartnerMerchantLinkState.Active)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidLinkStateTransition)
                .WithData("From", State.ToString())
                .WithData("Action", nameof(Suspend));
        }

        State = PartnerMerchantLinkState.Suspended;
    }

    public void Resume(DateTime atUtc)
    {
        EnsureNotTerminal();

        if (State != PartnerMerchantLinkState.Suspended)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidLinkStateTransition)
                .WithData("From", State.ToString())
                .WithData("Action", nameof(Resume));
        }

        State = SatisfiesActiveInvariant
            ? PartnerMerchantLinkState.Active
            : PartnerMerchantLinkState.ConsentPending;
    }

    /// <summary>Partner revokes the grant. Terminal — the link can never be re-activated. Re-revoking is a no-op.</summary>
    public void Revoke(Guid revokedBy, DateTime atUtc)
    {
        if (State == PartnerMerchantLinkState.Revoked)
        {
            return; // idempotent
        }

        if (State == PartnerMerchantLinkState.Terminated)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal)
                .WithData("State", State.ToString());
        }

        GrantState = GrantState.Revoked;
        State = PartnerMerchantLinkState.Revoked;
        RevokedBy = revokedBy;
        RevokedAt = atUtc;
    }

    /// <summary>
    /// Ends the relationship's LIFECYCLE (validity lapse / account closure / offboard). Terminal — the link
    /// can never be re-activated. Distinct from <see cref="Revoke"/>: Terminate is a lifecycle outcome with a
    /// recorded <see cref="TerminationReason"/> and does NOT mark the grant itself as revoked. Re-terminating
    /// is a no-op; terminating an already-Revoked link is rejected.
    /// </summary>
    public void Terminate(LinkTerminationReason reason, DateTime atUtc)
    {
        if (State == PartnerMerchantLinkState.Terminated)
        {
            return; // idempotent
        }

        if (State == PartnerMerchantLinkState.Revoked)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal)
                .WithData("State", State.ToString());
        }

        State = PartnerMerchantLinkState.Terminated;
        TerminationReason = reason;
        TerminatedAt = atUtc;
    }

    private bool IsWithinValidity(DateTime asOfUtc)
    {
        if (GrantedScope is null)
        {
            return false;
        }

        if (GrantedScope.ValidFrom is DateTime validFrom && asOfUtc < validFrom)
        {
            return false;
        }

        if (GrantedScope.ValidUntil is DateTime validUntil && asOfUtc > validUntil)
        {
            return false;
        }

        return true;
    }

    private bool SatisfiesActiveInvariant =>
        GrantState == GrantState.Offered &&
        GrantedScope is not null &&
        ConsentState == ConsentState.Given &&
        ConsentedGrantVersion == GrantVersion;

    private void ApplyConsent(int grantVersion, Guid consentedBy, DateTime atUtc)
    {
        ConsentState = ConsentState.Given;
        ConsentedGrantVersion = grantVersion;
        ConsentedBy = consentedBy;
        ConsentedAt = atUtc;
        State = PartnerMerchantLinkState.Active; // invariant holds: grant offered + versions match
    }

    private void EnsureNotTerminal()
    {
        if (State is PartnerMerchantLinkState.Revoked or PartnerMerchantLinkState.Terminated)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal)
                .WithData("State", State.ToString());
        }
    }
}
