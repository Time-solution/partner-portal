using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Zahy.Commission;

public class CommissionLedgerAppendOnlyTests : ZahyCommissionTestBase
{
    private readonly ICommissionLedgerService _ledgerService;
    private readonly IRepository<CommissionLedgerEntry, Guid> _ledgerRepository;
    private readonly CommissionTestCurrentUser _currentUser;

    public CommissionLedgerAppendOnlyTests()
    {
        _ledgerService = GetRequiredService<ICommissionLedgerService>();
        _ledgerRepository = GetRequiredService<IRepository<CommissionLedgerEntry, Guid>>();
        _currentUser = GetRequiredService<CommissionTestCurrentUser>();
    }

    [Fact]
    public async Task Duplicate_Source_And_Rule_Creates_One_Accrual()
    {
        var partnerId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var request = CreateAccrualRequest(partnerId, ruleId, 100m, 10m, CommissionDirection.PlatformEarns);

        CommissionLedgerAccrualResult first = null!;
        CommissionLedgerAccrualResult second = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _ledgerService.AccrueAsync(request);
            first.IsNew.ShouldBeTrue();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            second = await _ledgerService.AccrueAsync(request);
            second.IsNew.ShouldBeFalse();
            second.EntryId.ShouldBe(first.EntryId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = await _ledgerRepository.GetListAsync();
            rows.Count.ShouldBe(1);
            rows.Single().IdempotencyKey.ShouldBe(
                CommissionLedgerIdempotency.BuildAccrualKey(request.SourceType, request.SourceId, ruleId));
        });
    }

    [Fact]
    public async Task Reversal_Creates_Offsetting_Row_Original_Unchanged()
    {
        var partnerId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        CommissionLedgerAccrualResult accrual = null!;
        CommissionLedgerReversalResult reversal = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            _currentUser.UserId = Guid.NewGuid();
            accrual = await _ledgerService.AccrueAsync(
                CreateAccrualRequest(partnerId, ruleId, 250m, 25m, CommissionDirection.PartnerEarns));
            await _ledgerService.ApproveAsync(accrual.EntryId);
            _currentUser.UserId = Guid.NewGuid();
            await _ledgerService.MarkPaidAsync(accrual.EntryId);

            reversal = await _ledgerService.ReverseAsync(
                accrual.EntryId,
                new ReverseCommissionLedgerInput { Reason = "Order cancelled for append-only test" });
            reversal.IsNew.ShouldBeTrue();
            reversal.OriginalComputedCommission.ShouldBe(25m);
            reversal.ReversalComputedCommission.ShouldBe(-25m);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var original = await _ledgerRepository.GetAsync(accrual.EntryId);
            original.ComputedCommission.ShouldBe(25m);
            original.BasisAmount.ShouldBe(250m);
            original.Status.ShouldBe(CommissionLedgerStatus.Paid);
            original.EntryKind.ShouldBe(CommissionEntryKind.Accrual);

            var reversalEntry = await _ledgerRepository.GetAsync(reversal.ReversalEntryId);
            reversalEntry.EntryKind.ShouldBe(CommissionEntryKind.Reversal);
            reversalEntry.Status.ShouldBe(CommissionLedgerStatus.Reversed);
            reversalEntry.ComputedCommission.ShouldBe(-25m);
            reversalEntry.ReversesEntryId.ShouldBe(original.Id);
            reversalEntry.IdempotencyKey.ShouldBe(
                CommissionLedgerIdempotency.BuildReversalKey(
                    original.SourceType,
                    original.SourceId,
                    original.RuleId,
                    original.Id));

            var allRows = await _ledgerRepository.GetListAsync();
            allRows.Count.ShouldBe(2);
        });
    }

    [Fact]
    public async Task Should_Reject_Accrued_To_Paid_Skip()
    {
        var accrual = await AccrueInUowAsync(Guid.NewGuid(), Guid.NewGuid(), 50m, 5m);

        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _ledgerService.MarkPaidAsync(accrual.EntryId));

            ex.Code.ShouldBe(CommissionErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task Should_Reject_Mutation_Of_Basis_Amount()
    {
        var accrual = await AccrueInUowAsync(Guid.NewGuid(), Guid.NewGuid(), 100m, 10m);

        typeof(CommissionLedgerEntry)
            .GetProperty(nameof(CommissionLedgerEntry.BasisAmount))!
            .SetMethod!
            .IsPublic.ShouldBeFalse();

        typeof(CommissionLedgerEntry)
            .GetProperty(nameof(CommissionLedgerEntry.ComputedCommission))!
            .SetMethod!
            .IsPublic.ShouldBeFalse();

        await WithUnitOfWorkAsync(async () =>
        {
            _currentUser.UserId = Guid.NewGuid();
            await _ledgerService.ApproveAsync(accrual.EntryId);
            _currentUser.UserId = Guid.NewGuid();
            await _ledgerService.MarkPaidAsync(accrual.EntryId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entry = await _ledgerRepository.GetAsync(accrual.EntryId);
            entry.BasisAmount.ShouldBe(100m);
            entry.ComputedCommission.ShouldBe(10m);
            entry.Status.ShouldBe(CommissionLedgerStatus.Paid);
        });
    }

    [Fact]
    public async Task Should_Carry_Direction_On_Accrual_Row()
    {
        var partnerId = Guid.NewGuid();

        CommissionLedgerAccrualResult platformAccrual = null!;
        CommissionLedgerAccrualResult partnerAccrual = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            platformAccrual = await _ledgerService.AccrueAsync(CreateAccrualRequest(
                partnerId,
                Guid.NewGuid(),
                80m,
                8m,
                CommissionDirection.PlatformEarns,
                sourceId: "platform-direction-order"));

            partnerAccrual = await _ledgerService.AccrueAsync(CreateAccrualRequest(
                partnerId,
                Guid.NewGuid(),
                120m,
                12m,
                CommissionDirection.PartnerEarns,
                sourceId: "partner-direction-order"));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var platformRow = await _ledgerRepository.GetAsync(platformAccrual.EntryId);
            platformRow.Direction.ShouldBe(CommissionDirection.PlatformEarns);

            var partnerRow = await _ledgerRepository.GetAsync(partnerAccrual.EntryId);
            partnerRow.Direction.ShouldBe(CommissionDirection.PartnerEarns);
        });
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
                CreateAccrualRequest(partnerId, ruleId, basisAmount, computedCommission, CommissionDirection.PlatformEarns));
        });
        return result;
    }

    private static CommissionAccrualRequest CreateAccrualRequest(
        Guid partnerId,
        Guid ruleId,
        decimal basisAmount,
        decimal computedCommission,
        CommissionDirection direction,
        string sourceType = "order.paid",
        string? sourceId = null) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = Guid.NewGuid(),
            RuleId = ruleId,
            SourceType = sourceType,
            SourceId = sourceId ?? $"connector:aggregator:mock-aggregator:order-{ruleId:N}",
            OrderRecordId = Guid.NewGuid(),
            BasisAmount = basisAmount,
            ComputedCommission = computedCommission,
            Direction = direction,
            Currency = CommissionConsts.DefaultCurrency
        };
}
