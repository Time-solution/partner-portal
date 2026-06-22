using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Xunit;
using Zahy.Identity.Permissions;

namespace Zahy.Finance;

/// <summary>
/// Scope contract for <see cref="FinanceAccessGuard"/> (the finance-ledger read gate):
/// accountant/admin (Finance.ReadAll) see all; a partner sees only its OWN partner payable;
/// a merchant sees only its OWN tenant ledger; partner-ops (Partners.Manage) sees neither.
///
/// These are pure unit tests because the integration host uses AddAlwaysAllowAuthorization(),
/// which grants every permission and therefore cannot exercise permission-specific scoping.
/// </summary>
public class FinanceAccessGuardTests
{
    private static readonly Guid PartnerA = Guid.NewGuid();
    private static readonly Guid PartnerB = Guid.NewGuid();
    private static readonly Guid MerchantA = Guid.NewGuid();
    private static readonly Guid MerchantB = Guid.NewGuid();

    private static FinanceAccessGuard Guard(
        Guid? currentPartnerId = null,
        Guid? currentTenantId = null,
        params string[] grantedPermissions) =>
        new(
            new TestCurrentPartner { Id = currentPartnerId },
            new TestCurrentTenant { Id = currentTenantId },
            new FakePermissionChecker(grantedPermissions));

    // ── The leak: partner-ops (Partners.Manage) must NOT read a merchant ledger ──────────────

    [Fact]
    public async Task PartnerOps_With_PartnersManage_Cannot_Read_A_Merchant_Ledger()
    {
        var guard = Guard(grantedPermissions: ZahyPermissions.Partners.Manage);

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            guard.EnsureCanAccessMerchantAccountAsync(MerchantA));
    }

    [Fact]
    public async Task PartnerOps_With_PartnersManage_Cannot_Read_An_Arbitrary_Partner_Ledger()
    {
        var guard = Guard(grantedPermissions: ZahyPermissions.Partners.Manage);

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            guard.EnsureCanAccessPartnerAccountAsync(PartnerA));
    }

    // ── Accountant / admin (Finance.ReadAll) see all ─────────────────────────────────────────

    [Fact]
    public async Task Accountant_With_FinanceReadAll_Can_Read_Any_Merchant_Ledger()
    {
        var guard = Guard(grantedPermissions: ZahyPermissions.Finance.ReadAll);

        await Should.NotThrowAsync(() => guard.EnsureCanAccessMerchantAccountAsync(MerchantA));
        await Should.NotThrowAsync(() => guard.EnsureCanAccessMerchantAccountAsync(MerchantB));
    }

    [Fact]
    public async Task Admin_With_FinanceReadAll_Can_Read_Any_Partner_Ledger()
    {
        var guard = Guard(grantedPermissions: ZahyPermissions.Finance.ReadAll);

        await Should.NotThrowAsync(() => guard.EnsureCanAccessPartnerAccountAsync(PartnerA));
        await Should.NotThrowAsync(() => guard.EnsureCanAccessPartnerAccountAsync(PartnerB));
    }

    // ── Partner sees only own payable ────────────────────────────────────────────────────────

    [Fact]
    public async Task Partner_Sees_Only_Its_Own_Payable()
    {
        var guard = Guard(currentPartnerId: PartnerA);

        await Should.NotThrowAsync(() => guard.EnsureCanAccessPartnerAccountAsync(PartnerA));
        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            guard.EnsureCanAccessPartnerAccountAsync(PartnerB));
    }

    [Fact]
    public async Task Partner_Cannot_Read_A_Merchant_Ledger_Even_Its_Own_Marketplace_Merchant()
    {
        // A partner principal has no tenant context and no Finance.ReadAll → no merchant-ledger access.
        var guard = Guard(currentPartnerId: PartnerA);

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            guard.EnsureCanAccessMerchantAccountAsync(MerchantA));
    }

    // ── Merchant sees only own ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Merchant_Sees_Only_Its_Own_Ledger()
    {
        var guard = Guard(currentTenantId: MerchantA);

        await Should.NotThrowAsync(() => guard.EnsureCanAccessMerchantAccountAsync(MerchantA));
        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            guard.EnsureCanAccessMerchantAccountAsync(MerchantB));
    }

    [Fact]
    public async Task A_Principal_With_No_Context_And_No_ReadAll_Is_Denied()
    {
        var guard = Guard();

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            guard.EnsureCanAccessMerchantAccountAsync(MerchantA));
        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            guard.EnsureCanAccessPartnerAccountAsync(PartnerA));
    }

    private sealed class FakePermissionChecker : IPermissionChecker
    {
        private readonly HashSet<string> _granted;

        public FakePermissionChecker(params string[] granted) =>
            _granted = new HashSet<string>(granted ?? Array.Empty<string>(), StringComparer.Ordinal);

        public Task<bool> IsGrantedAsync(string name) =>
            Task.FromResult(_granted.Contains(name));

        public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name) =>
            Task.FromResult(_granted.Contains(name));

        public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names) =>
            throw new NotSupportedException();

        public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names) =>
            throw new NotSupportedException();
    }
}
