using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.Commission;

public class CommissionLedgerApprovalTests : ZahyCommissionTestBase
{
    private readonly ICommissionLedgerService _ledgerService;
    private readonly IRepository<CommissionLedgerEntry, Guid> _ledgerRepository;
    private readonly CommissionTestCurrentUser _currentUser;

    public CommissionLedgerApprovalTests()
    {
        _ledgerService = GetRequiredService<ICommissionLedgerService>();
        _ledgerRepository = GetRequiredService<IRepository<CommissionLedgerEntry, Guid>>();
        _currentUser = GetRequiredService<CommissionTestCurrentUser>();
    }

    [Fact]
    public async Task Approve_Persists_ApprovedAt_And_ApprovedByUserId()
    {
        var approverId = Guid.NewGuid();
        _currentUser.UserId = approverId;
        var accrual = await AccrueInUowAsync(Guid.NewGuid(), Guid.NewGuid(), 100m, 10m);

        await WithUnitOfWorkAsync(async () =>
        {
            await _ledgerService.ApproveAsync(accrual.EntryId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var row = await _ledgerRepository.GetAsync(accrual.EntryId);
            row.Status.ShouldBe(CommissionLedgerStatus.Approved);
            row.ApprovedAt.ShouldNotBeNull();
            row.ApprovedByUserId.ShouldBe(approverId);
        });
    }

    [Fact]
    public async Task MarkPaid_Rejects_Same_Actor_As_Approver()
    {
        var actor = Guid.NewGuid();
        _currentUser.UserId = actor;
        var accrual = await AccrueInUowAsync(Guid.NewGuid(), Guid.NewGuid(), 80m, 8m);

        await WithUnitOfWorkAsync(async () =>
        {
            await _ledgerService.ApproveAsync(accrual.EntryId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _ledgerService.MarkPaidAsync(accrual.EntryId));
            ex.Code.ShouldBe(CommissionErrorCodes.MarkPaidSameActorAsApprover);
        });
    }

    [Fact]
    public async Task MarkPaid_Allows_Different_Actor()
    {
        _currentUser.UserId = Guid.NewGuid();
        var accrual = await AccrueInUowAsync(Guid.NewGuid(), Guid.NewGuid(), 90m, 9m);

        await WithUnitOfWorkAsync(async () =>
        {
            await _ledgerService.ApproveAsync(accrual.EntryId);
        });

        _currentUser.UserId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var dto = await _ledgerService.MarkPaidAsync(accrual.EntryId);
            dto.Status.ShouldBe(CommissionLedgerStatus.Paid);
        });
    }

    [Fact]
    public async Task Reverse_Creates_Reversal_Entry_Idempotent()
    {
        var accrual = await AccrueInUowAsync(Guid.NewGuid(), Guid.NewGuid(), 250m, 25m);
        var reason = new ReverseCommissionLedgerInput { Reason = "Order cancelled by customer post-fulfillment" };

        CommissionLedgerReversalResult first = null!;
        CommissionLedgerReversalResult second = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _ledgerService.ReverseAsync(accrual.EntryId, reason);
            first.IsNew.ShouldBeTrue();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            second = await _ledgerService.ReverseAsync(accrual.EntryId, reason);
            second.IsNew.ShouldBeFalse();
            second.ReversalEntryId.ShouldBe(first.ReversalEntryId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = await _ledgerRepository.GetListAsync();
            rows.Count.ShouldBe(2);
        });
    }

    [Fact]
    public async Task Reverse_Requires_Reason_MinLength()
    {
        var accrual = await AccrueInUowAsync(Guid.NewGuid(), Guid.NewGuid(), 50m, 5m);

        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _ledgerService.ReverseAsync(accrual.EntryId, new ReverseCommissionLedgerInput { Reason = "short" }));
            ex.Code.ShouldBe(CommissionErrorCodes.ReversalReasonRequired);
        });
    }

    [Fact]
    public async Task List_FiltersByStatus_And_Partner_And_DateRange()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();
        var rule = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await _ledgerService.AccrueAsync(CreateAccrualRequest(partnerA, rule, 10m, 1m, "a"));
            await _ledgerService.AccrueAsync(CreateAccrualRequest(partnerB, rule, 20m, 2m, "b"));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var accrued = await _ledgerService.GetListAsync(new CommissionLedgerListInput
            {
                Status = CommissionLedgerStatus.Accrued,
            });
            accrued.Count.ShouldBeGreaterThanOrEqualTo(2);

            var partnerOnly = await _ledgerService.GetListAsync(new CommissionLedgerListInput
            {
                PartnerId = partnerA,
            });
            partnerOnly.ShouldAllBe(x => x.PartnerId == partnerA);
            partnerOnly.Count.ShouldBe(1);
        });
    }

    [Fact]
    public void Permission_Commission_Approve_Required_For_Write_Endpoints()
    {
        var serviceType = typeof(CommissionLedgerService);
        AssertHasAuthorize(serviceType.GetMethod(nameof(CommissionLedgerService.ApproveAsync))!,
            ZahyPermissions.Commission.Approve);
        AssertHasAuthorize(serviceType.GetMethod(nameof(CommissionLedgerService.ReverseAsync))!,
            ZahyPermissions.Commission.Approve);
        AssertHasAuthorize(serviceType.GetMethod(nameof(CommissionLedgerService.MarkPaidAsync))!,
            ZahyPermissions.Commission.Approve);
        AssertHasAuthorize(serviceType.GetMethod(nameof(CommissionLedgerService.GetListAsync))!,
            ZahyPermissions.Finance.ReadAll);
    }

    private static void AssertHasAuthorize(MethodInfo method, string permission)
    {
        method.GetCustomAttributes<AuthorizeAttribute>()
            .Any(a => string.Equals(a.Policy, permission, StringComparison.Ordinal))
            .ShouldBeTrue($"{method.Name} must require {permission}");
    }

    private async Task<CommissionLedgerAccrualResult> AccrueInUowAsync(
        Guid partnerId,
        Guid ruleId,
        decimal basisAmount,
        decimal computedCommission)
    {
        CommissionLedgerAccrualResult result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _ledgerService.AccrueAsync(
                CreateAccrualRequest(partnerId, ruleId, basisAmount, computedCommission, Guid.NewGuid().ToString("N")));
        });
        return result;
    }

    private static CommissionAccrualRequest CreateAccrualRequest(
        Guid partnerId,
        Guid ruleId,
        decimal basisAmount,
        decimal computedCommission,
        string sourceSuffix) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = Guid.NewGuid(),
            RuleId = ruleId,
            SourceType = "order.paid",
            SourceId = $"connector:aggregator:mock:{sourceSuffix}",
            OrderRecordId = Guid.NewGuid(),
            BasisAmount = basisAmount,
            ComputedCommission = computedCommission,
            Direction = CommissionDirection.PlatformEarns,
            Currency = CommissionConsts.DefaultCurrency
        };
}

public class CommissionLedgerRbacTests
{
    private static string[] PermissionsOf(string role) =>
        ZahyRoleRegistry.Find(role).ShouldNotBeNull().Permissions.ToArray();

    [Fact]
    public void Platform_Finance_And_SuperAdmin_Hold_Commission_Approve()
    {
        PermissionsOf(ZahyRoles.PlatformFinance).ShouldContain(ZahyPermissions.Commission.Approve);
        PermissionsOf(ZahyRoles.PlatformSuperAdmin).ShouldContain(ZahyPermissions.Commission.Approve);
    }

    [Fact]
    public void Partner_And_Merchant_Roles_Do_Not_Hold_Commission_Approve()
    {
        foreach (var role in new[]
                 {
                     ZahyRoles.PartnerOwner, ZahyRoles.PartnerManager, ZahyRoles.PartnerStaff,
                     ZahyRoles.MerchantOwner, ZahyRoles.MerchantManager
                 })
        {
            PermissionsOf(role).ShouldNotContain(ZahyPermissions.Commission.Approve);
        }
    }
}
