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
using Zahy.Settlement.Read;
using Zahy.Settlement.Write;

namespace Zahy.Settlement;

public class SettlementWriteAppServiceTests : ZahySettlementReadTestBase
{
    private static readonly Guid PartnerA = Guid.NewGuid();

    private readonly ISettlementWriteAppService _writeService;
    private readonly IRepository<SettlementCase, Guid> _caseRepository;
    private readonly ISettlementEventStore _eventStore;
    private readonly RecordingSettlementAuditLogger _auditLogger;
    private readonly SettlementTestCurrentUser _currentUser;

    public SettlementWriteAppServiceTests()
    {
        _writeService = GetRequiredService<ISettlementWriteAppService>();
        _caseRepository = GetRequiredService<IRepository<SettlementCase, Guid>>();
        _eventStore = GetRequiredService<ISettlementEventStore>();
        _auditLogger = GetRequiredService<RecordingSettlementAuditLogger>();
        _currentUser = GetRequiredService<SettlementTestCurrentUser>();
    }

    [Fact]
    public async Task Trigger_Reversal_Creates_InvertedCase_NetsToZero()
    {
        var original = await SeedOriginalAsync();

        SettlementCaseReadDto reversal = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            reversal = await _writeService.TriggerCaseReversalAsync(new TriggerReversalRequest
            {
                OriginalSettlementCaseId = original.Id,
                Reason = "Salasa wrongly invoiced — duplicate disbursement",
            });
        });

        reversal.ReversesSettlementCaseId.ShouldBe(original.Id);
        reversal.Journal.ShouldNotBeNull();

        // Original: Dr AggregatorClearing 113 / Cr MerchantPayable 100 / Cr VatOutput 13.
        // Reversal mirrors every leg, so each account nets to zero.
        var net = NetByAccount(original.Journal!.Lines.Concat(reversal.Journal!.Lines));
        net.Values.ShouldAllBe(v => v == 0m);

        reversal.Journal!.TotalDebits.Amount.ShouldBe(original.Journal!.TotalCredits.Amount);
        reversal.Journal!.TotalCredits.Amount.ShouldBe(original.Journal!.TotalDebits.Amount);
    }

    [Fact]
    public async Task Trigger_Reversal_Idempotent_Same_Key_Returns_Same_Case()
    {
        var original = await SeedOriginalAsync();
        var request = new TriggerReversalRequest
        {
            OriginalSettlementCaseId = original.Id,
            Reason = "Duplicate disbursement correction applied",
            IdempotencyKey = "rev-key-001",
        };

        SettlementCaseReadDto first = null!;
        SettlementCaseReadDto second = null!;
        await WithUnitOfWorkAsync(async () => first = await _writeService.TriggerCaseReversalAsync(request));
        await WithUnitOfWorkAsync(async () => second = await _writeService.TriggerCaseReversalAsync(request));

        second.Id.ShouldBe(first.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            var reversals = await _caseRepository.GetListAsync(x => x.ReversesSettlementCaseId == original.Id);
            reversals.Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Trigger_Reversal_Rejected_When_Already_Reversed()
    {
        var original = await SeedOriginalAsync();

        await WithUnitOfWorkAsync(async () => await _writeService.TriggerCaseReversalAsync(new TriggerReversalRequest
        {
            OriginalSettlementCaseId = original.Id,
            Reason = "First reversal for this case fully applied",
            IdempotencyKey = "key-A",
        }));

        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _writeService.TriggerCaseReversalAsync(new TriggerReversalRequest
                {
                    OriginalSettlementCaseId = original.Id,
                    Reason = "Second distinct reversal attempt on same case",
                    IdempotencyKey = "key-B",
                }));
            ex.Code.ShouldBe(SettlementReversalErrorCodes.OriginalNotReversible);
        });
    }

    [Fact]
    public async Task Trigger_Reversal_Rejected_When_Reason_Empty()
    {
        var original = await SeedOriginalAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _writeService.TriggerCaseReversalAsync(new TriggerReversalRequest
                {
                    OriginalSettlementCaseId = original.Id,
                    Reason = "short",
                }));
            ex.Code.ShouldBe(SettlementReversalErrorCodes.ReasonRequired);
        });
    }

    [Fact]
    public async Task Trigger_Reversal_Persists_Reason_And_Actor()
    {
        var actor = Guid.NewGuid();
        _currentUser.UserId = actor;
        var original = await SeedOriginalAsync();
        const string reason = "Customer cancelled order after settlement allocated";

        SettlementCaseReadDto reversal = null!;
        await WithUnitOfWorkAsync(async () => reversal = await _writeService.TriggerCaseReversalAsync(new TriggerReversalRequest
        {
            OriginalSettlementCaseId = original.Id,
            Reason = reason,
        }));

        await WithUnitOfWorkAsync(async () =>
        {
            var row = await _caseRepository.GetAsync(reversal.Id);
            row.Reason.ShouldBe(reason);
            row.ReversedByUserId.ShouldBe(actor);
            row.ReversesSettlementCaseId.ShouldBe(original.Id);
        });
    }

    [Fact]
    public async Task Trigger_Reversal_Audit_Log_Written()
    {
        var original = await SeedOriginalAsync();

        await WithUnitOfWorkAsync(async () => await _writeService.TriggerCaseReversalAsync(new TriggerReversalRequest
        {
            OriginalSettlementCaseId = original.Id,
            Reason = "Reconciliation mismatch requires reversal now",
        }));

        var entry = _auditLogger.Entries.ShouldHaveSingleItem();
        entry.Action.ShouldBe("Settlement.TriggerCaseReversal");
        entry.TargetId.ShouldBe(original.Id.ToString());
        entry.ExtraData.ShouldContain("Reconciliation mismatch");
    }

    [Fact]
    public void Trigger_Reversal_Permission_Required()
    {
        var method = typeof(SettlementWriteAppService)
            .GetMethod(nameof(SettlementWriteAppService.TriggerCaseReversalAsync))!;

        method.GetCustomAttributes<AuthorizeAttribute>()
            .Any(a => string.Equals(a.Policy, ZahyPermissions.Settlement.Disburse, StringComparison.Ordinal))
            .ShouldBeTrue("TriggerCaseReversalAsync must require Settlement.Disburse");
    }

    private async Task<SettlementCaseReadDto> SeedOriginalAsync()
    {
        var id = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            var original = SettlementCase.Start(
                id, SettlementBook.Marketplace, PartnerA, $"order:orig:{id:N}", DateTime.UtcNow);
            original.TransitionTo(SettlementCaseState.Allocated, DateTime.UtcNow);
            await _caseRepository.InsertAsync(original, autoSave: true);

            var snapshot = new SettlementAllocationSnapshot
            {
                Currency = "SAR",
                MerchantPayout = 100m,
                VatOutput = 13m,
                NetVatToZatca = 13m,
                TotalDebits = 113m,
                TotalCredits = 113m,
                Legs = new List<SettlementJournalLegDto>
                {
                    new() { Account = nameof(SettlementAccountType.AggregatorClearing), Direction = "Debit", Amount = 113m },
                    new() { Account = nameof(SettlementAccountType.MerchantPayable), Direction = "Credit", Amount = 100m },
                    new() { Account = nameof(SettlementAccountType.VatOutput), Direction = "Credit", Amount = 13m },
                },
            };

            await _eventStore.InsertAsync(SettlementWebhookEvent.Processed(
                Guid.NewGuid(), SettlementBook.Marketplace, PartnerA, $"order:orig:{id:N}", "{}",
                WebhookSignatureStatus.Valid, SettlementEventOutcome.Allocated, id,
                SettlementCaseState.Allocated, snapshot.ToJson(), DateTime.UtcNow));
        });

        SettlementCaseReadDto dto = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            var read = GetRequiredService<ISettlementReadAppService>();
            dto = (await read.GetCasesAsync(new SettlementPartnerQuery { PartnerId = PartnerA }))
                .Single(c => c.Id == id);
        });
        return dto;
    }

    private static Dictionary<string, decimal> NetByAccount(IEnumerable<SettlementJournalLineReadDto> lines)
    {
        var net = new Dictionary<string, decimal>();
        foreach (var line in lines)
        {
            var signed = line.Direction == EntryDirection.Debit ? line.Amount.Amount : -line.Amount.Amount;
            var key = line.Account.ToString();
            net[key] = net.TryGetValue(key, out var running) ? running + signed : signed;
        }

        return net;
    }
}
