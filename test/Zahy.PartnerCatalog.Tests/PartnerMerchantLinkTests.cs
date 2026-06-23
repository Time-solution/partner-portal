using System;
using System.Collections.Generic;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.PartnerCatalog;

/// <summary>
/// R1 spike — PURE DOMAIN proof for the partner&lt;-&gt;merchant participation link (grant + consent, PDPL).
/// No persistence / app service / wiring; just the aggregate + value-object invariants.
/// </summary>
public class PartnerMerchantLinkTests
{
    private static readonly Guid PartnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MerchantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PartnerUser = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MerchantUser = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PartnerMerchantLink NewLink() =>
        PartnerMerchantLink.Create(Guid.NewGuid(), PartnerId, MerchantId);

    private static GrantedScope ScopeV1() => GrantedScope.Create(
        offerings: new[] { "menu", "delivery" },
        dataFieldsToPartner: new[] { "order.items", "order.total" },
        dataFieldsToMerchant: new[] { "partner.eta" },
        allowedActions: new[] { "read", "fulfil" },
        zones: new[] { "riyadh" },
        limits: new Dictionary<string, decimal> { ["MonthlyOrders"] = 5000m });

    private static GrantedScope ScopeV2() => GrantedScope.Create(
        offerings: new[] { "menu", "delivery", "pickup" }, // real content change
        dataFieldsToPartner: new[] { "order.items", "order.total" },
        dataFieldsToMerchant: new[] { "partner.eta" },
        allowedActions: new[] { "read", "fulfil" },
        zones: new[] { "riyadh" },
        limits: new Dictionary<string, decimal> { ["MonthlyOrders"] = 5000m });

    [Fact]
    public void Active_Requires_Both_Live_Grant_And_Consent_Matching_Current_Version()
    {
        var link = NewLink();

        // Grant alone is not enough.
        link.OfferGrant(ScopeV1(), PartnerUser, T0);
        link.State.ShouldBe(PartnerMerchantLinkState.GrantOffered);
        link.IsActive.ShouldBeFalse();

        // Grant + matching consent ⇒ Active.
        link.GiveConsent(link.GrantVersion, MerchantUser, T0);
        link.State.ShouldBe(PartnerMerchantLinkState.Active);
        link.IsActive.ShouldBeTrue();
        link.ConsentedGrantVersion.ShouldBe(link.GrantVersion);
    }

    [Fact]
    public void ReviseGrant_Bumps_Version_Drops_To_ConsentPending_Then_ReConsent_Active()
    {
        var link = NewLink();
        link.OfferGrant(ScopeV1(), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);
        link.IsActive.ShouldBeTrue();

        link.ReviseGrant(ScopeV2(), PartnerUser, T0);
        link.GrantVersion.ShouldBe(2);
        link.State.ShouldBe(PartnerMerchantLinkState.ConsentPending);
        link.IsActive.ShouldBeFalse();

        link.GiveConsent(2, MerchantUser, T0);
        link.IsActive.ShouldBeTrue();
        link.State.ShouldBe(PartnerMerchantLinkState.Active);
    }

    [Fact]
    public void Consent_Pinned_To_Old_Version_Does_Not_Activate_After_Revise()
    {
        var link = NewLink();
        link.OfferGrant(ScopeV1(), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);
        link.ReviseGrant(ScopeV2(), PartnerUser, T0); // now version 2; consent still pinned to 1

        link.ConsentedGrantVersion.ShouldBe(1);
        link.GrantVersion.ShouldBe(2);
        link.IsActive.ShouldBeFalse(); // the PDPL invariant: stale consent never grants access

        // Re-consenting to the SUPERSEDED version is rejected.
        Should.Throw<BusinessException>(() => link.GiveConsent(1, MerchantUser, T0))
            .Code.ShouldBe(PartnerCatalogErrorCodes.StaleConsentVersion);
    }

    [Fact]
    public void WithdrawConsent_Drops_Active()
    {
        var link = NewLink();
        link.OfferGrant(ScopeV1(), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);
        link.IsActive.ShouldBeTrue();

        link.WithdrawConsent(T0);
        link.IsActive.ShouldBeFalse();
        link.State.ShouldBe(PartnerMerchantLinkState.ConsentPending);
        link.ConsentState.ShouldBe(ConsentState.Withdrawn);
    }

    [Fact]
    public void Revoke_Is_Terminal_Cannot_Reactivate()
    {
        var link = NewLink();
        link.OfferGrant(ScopeV1(), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);

        link.Revoke(PartnerUser, T0);
        link.State.ShouldBe(PartnerMerchantLinkState.Revoked);
        link.IsActive.ShouldBeFalse();

        // No path back to Active.
        Should.Throw<BusinessException>(() => link.GiveConsent(1, MerchantUser, T0))
            .Code.ShouldBe(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal);
        Should.Throw<BusinessException>(() => link.ReviseGrant(ScopeV2(), PartnerUser, T0))
            .Code.ShouldBe(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal);
        Should.Throw<BusinessException>(() => link.Resume(T0))
            .Code.ShouldBe(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal);
    }

    [Fact]
    public void Idempotent_ReGiveConsent_And_ReRevoke_Are_NoOps()
    {
        var link = NewLink();
        link.OfferGrant(ScopeV1(), PartnerUser, T0);

        link.GiveConsent(1, MerchantUser, T0);
        var consentedAt = link.ConsentedAt;
        link.GiveConsent(1, MerchantUser, T0.AddHours(1)); // same version → no-op
        link.IsActive.ShouldBeTrue();
        link.ConsentedAt.ShouldBe(consentedAt); // unchanged

        link.Revoke(PartnerUser, T0);
        var revokedAt = link.RevokedAt;
        link.Revoke(PartnerUser, T0.AddHours(2)); // re-revoke → no-op
        link.State.ShouldBe(PartnerMerchantLinkState.Revoked);
        link.RevokedAt.ShouldBe(revokedAt);
    }

    [Fact]
    public void ReviseGrant_With_Identical_Scope_Is_NoOp_No_Version_Bump()
    {
        var link = NewLink();
        link.OfferGrant(ScopeV1(), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);
        link.IsActive.ShouldBeTrue();

        // A revise that carries the SAME content (rebuilt, reordered inputs) must not bump or de-activate.
        var sameContentReordered = GrantedScope.Create(
            offerings: new[] { "delivery", "menu" }, // reordered
            dataFieldsToPartner: new[] { "order.total", "order.items" },
            dataFieldsToMerchant: new[] { "partner.eta" },
            allowedActions: new[] { "fulfil", "read" },
            zones: new[] { "riyadh" },
            limits: new Dictionary<string, decimal> { ["MonthlyOrders"] = 5000m });

        link.ReviseGrant(sameContentReordered, PartnerUser, T0.AddDays(1));
        link.GrantVersion.ShouldBe(1); // no bump
        link.IsActive.ShouldBeTrue();  // still active
    }

    [Fact]
    public void GrantedScope_Hash_Stable_On_NoOp_Changes_On_Real_Change()
    {
        var a = ScopeV1();

        // Same content, different input order ⇒ same hash (and value-equal).
        var aReordered = GrantedScope.Create(
            offerings: new[] { "delivery", "menu" },
            dataFieldsToPartner: new[] { "order.total", "order.items" },
            dataFieldsToMerchant: new[] { "partner.eta" },
            allowedActions: new[] { "fulfil", "read" },
            zones: new[] { "riyadh" },
            limits: new Dictionary<string, decimal> { ["MonthlyOrders"] = 5000m });
        aReordered.Hash.ShouldBe(a.Hash);
        aReordered.ShouldBe(a);

        // Real content change ⇒ different hash.
        ScopeV2().Hash.ShouldNotBe(a.Hash);

        // A changed limit value is a real change too.
        var changedLimit = GrantedScope.Create(
            offerings: new[] { "menu", "delivery" },
            dataFieldsToPartner: new[] { "order.items", "order.total" },
            dataFieldsToMerchant: new[] { "partner.eta" },
            allowedActions: new[] { "read", "fulfil" },
            zones: new[] { "riyadh" },
            limits: new Dictionary<string, decimal> { ["MonthlyOrders"] = 6000m });
        changedLimit.Hash.ShouldNotBe(a.Hash);
    }

    [Fact]
    public void StandingGrant_With_ActivationAsConsent_Yields_Active()
    {
        var link = NewLink();
        var publicScope = GrantedScope.Create(
            offerings: new[] { "public-menu" },
            allowedActions: new[] { "read" },
            isStandingGrant: true);

        link.OfferGrant(publicScope, PartnerUser, T0);

        // The merchant's activation stands in as consent for a PUBLIC/standing grant.
        link.AcceptByActivation(MerchantUser, T0);
        link.IsActive.ShouldBeTrue();
        link.ConsentedGrantVersion.ShouldBe(link.GrantVersion);

        // The activation-as-consent shortcut is rejected for a PRIVATE grant.
        var privateLink = NewLink();
        privateLink.OfferGrant(ScopeV1(), PartnerUser, T0); // ScopeV1 is not standing
        Should.Throw<BusinessException>(() => privateLink.AcceptByActivation(MerchantUser, T0))
            .Code.ShouldBe(PartnerCatalogErrorCodes.StandingGrantRequired);
    }

    // --- Addendum: Terminate + validity/expiry ---

    private static GrantedScope ScopeWithWindow(DateTime? from, DateTime? until) => GrantedScope.Create(
        offerings: new[] { "menu" },
        allowedActions: new[] { "read" },
        validFrom: from,
        validUntil: until);

    [Fact]
    public void Terminate_Is_Terminal_And_Distinct_From_Revoke()
    {
        var link = NewLink();
        link.OfferGrant(ScopeV1(), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);
        link.IsActive.ShouldBeTrue();

        link.Terminate(LinkTerminationReason.AccountClosure, T0.AddDays(5));

        // Terminal lifecycle end — records a reason, NOT a revocation.
        link.State.ShouldBe(PartnerMerchantLinkState.Terminated);
        link.IsActive.ShouldBeFalse();
        link.TerminationReason.ShouldBe(LinkTerminationReason.AccountClosure);
        link.TerminatedAt.ShouldBe(T0.AddDays(5));
        link.RevokedBy.ShouldBeNull();          // distinct from Revoke
        link.RevokedAt.ShouldBeNull();
        link.GrantState.ShouldBe(GrantState.Offered); // the grant was not revoked, the lifecycle ended

        // Terminal exactly like Revoked: cannot reactivate / consent / revise.
        Should.Throw<BusinessException>(() => link.GiveConsent(1, MerchantUser, T0))
            .Code.ShouldBe(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal);
        Should.Throw<BusinessException>(() => link.ReviseGrant(ScopeV2(), PartnerUser, T0))
            .Code.ShouldBe(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal);

        // Re-terminate is a no-op; revoking an already-terminated link is rejected.
        link.Terminate(LinkTerminationReason.Offboard, T0.AddDays(6));
        link.TerminationReason.ShouldBe(LinkTerminationReason.AccountClosure); // unchanged
        Should.Throw<BusinessException>(() => link.Revoke(PartnerUser, T0.AddDays(7)))
            .Code.ShouldBe(PartnerCatalogErrorCodes.PartnerMerchantLinkTerminal);

        // Contrast: a revoked link records the party, not a termination reason.
        var revoked = NewLink();
        revoked.OfferGrant(ScopeV1(), PartnerUser, T0);
        revoked.GiveConsent(1, MerchantUser, T0);
        revoked.Revoke(PartnerUser, T0);
        revoked.State.ShouldBe(PartnerMerchantLinkState.Revoked);
        revoked.RevokedBy.ShouldBe(PartnerUser);
        revoked.TerminationReason.ShouldBeNull();
    }

    [Fact]
    public void Expired_Grant_Suppresses_IsActive_While_State_Stays_Active()
    {
        var windowEnd = T0.AddDays(30);
        var link = NewLink();
        link.OfferGrant(ScopeWithWindow(T0, windowEnd), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);

        // Inside the window: active. After ValidUntil: IsActive is suppressed, but State is NOT auto-flipped.
        link.IsActiveAsOf(T0.AddDays(10)).ShouldBeTrue();
        link.IsActiveAsOf(windowEnd.AddDays(1)).ShouldBeFalse();
        link.IsExpiredAsOf(windowEnd.AddDays(1)).ShouldBeTrue();
        link.State.ShouldBe(PartnerMerchantLinkState.Active); // no time-based side effect

        // Even after suspend/resume the state is non-terminal; expiry still suppresses IsActive.
        link.Suspend(windowEnd.AddDays(1));
        link.Resume(windowEnd.AddDays(2));
        link.State.ShouldBe(PartnerMerchantLinkState.Active);
        link.IsActiveAsOf(windowEnd.AddDays(2)).ShouldBeFalse();

        // A grant whose ValidUntil is in the real past also suppresses the parameterless (UtcNow) IsActive.
        var pastLink = NewLink();
        pastLink.OfferGrant(ScopeWithWindow(null, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)), PartnerUser, T0);
        pastLink.GiveConsent(1, MerchantUser, T0);
        pastLink.IsActive.ShouldBeFalse();
        pastLink.State.ShouldBe(PartnerMerchantLinkState.Active);

        // The terminal state is only recorded by an explicit Terminate(ValidityLapsed).
        link.Terminate(LinkTerminationReason.ValidityLapsed, windowEnd.AddDays(3));
        link.State.ShouldBe(PartnerMerchantLinkState.Terminated);
        link.TerminationReason.ShouldBe(LinkTerminationReason.ValidityLapsed);
    }

    [Fact]
    public void ReConsent_After_Expiry_Restores_Active_Within_New_Window()
    {
        var firstEnd = T0.AddDays(30);
        var link = NewLink();
        link.OfferGrant(ScopeWithWindow(T0, firstEnd), PartnerUser, T0);
        link.GiveConsent(1, MerchantUser, T0);

        // Window lapses → not active (state still Active, no side effect).
        var afterExpiry = firstEnd.AddDays(1);
        link.IsActiveAsOf(afterExpiry).ShouldBeFalse();

        // Partner re-grants with an extended window (real content change → version 2 → ConsentPending).
        var secondEnd = firstEnd.AddDays(60);
        link.ReviseGrant(ScopeWithWindow(T0, secondEnd), PartnerUser, afterExpiry);
        link.GrantVersion.ShouldBe(2);
        link.State.ShouldBe(PartnerMerchantLinkState.ConsentPending);
        link.IsActiveAsOf(afterExpiry).ShouldBeFalse(); // consent still pinned to v1

        // Merchant re-consents to the new version → active again within the new window.
        link.GiveConsent(2, MerchantUser, afterExpiry);
        link.IsActiveAsOf(afterExpiry).ShouldBeTrue();
        link.IsActiveAsOf(secondEnd.AddDays(1)).ShouldBeFalse(); // and expires again at the new boundary
    }
}
