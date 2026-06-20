using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Zahy.Settlement;

/// <summary>
/// Principal resale trigger — posts balanced journals in-memory and advances cases to Allocated only.
/// Real disbursement and live ZATCA remain OFF via <see cref="SettlementEngineOptions"/>.
/// </summary>
public sealed class SettlementResaleTriggerService : ISettlementResaleTriggerPort, ITransientDependency
{
    private readonly IResaleVatCalculator _vatCalculator;
    private readonly ISettlementCaseStore _caseStore;
    private readonly ISettlementFlowProfileResolver _profileResolver;

    public SettlementResaleTriggerService(
        IResaleVatCalculator vatCalculator,
        ISettlementCaseStore caseStore,
        ISettlementFlowProfileResolver profileResolver)
    {
        _vatCalculator = vatCalculator;
        _caseStore = caseStore;
        _profileResolver = profileResolver;
    }

    public async Task<SettlementResaleTriggerResult> TriggerPrincipalResaleAsync(
        SettlementResaleTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _caseStore.FindByKeyAsync(request.Book, request.ExternalTransactionId, cancellationToken);
        if (existing != null)
        {
            return BuildDuplicateResult(existing);
        }

        var line = CostMarkupLine.Of(request.BuyPrice, request.SellPrice);
        var vat = _vatCalculator.Compute(line, request.VatRate, VatTreatment.Principal);
        var profile = _profileResolver.Resolve(request.Book);
        var journal = PrincipalResaleJournalBuilder.Build(
            line,
            vat,
            request.Book,
            request.PostedAt,
            Guid.NewGuid(),
            request.Reference);

        BookIsolationGuard.EnsureWithinBook(profile, journal);

        var settlementCase = SettlementCase.Start(
            Guid.NewGuid(),
            request.Book,
            request.PartnerId,
            request.ExternalTransactionId,
            request.PostedAt);
        settlementCase.TransitionTo(SettlementCaseState.Allocated, request.PostedAt);
        await _caseStore.InsertAsync(settlementCase, cancellationToken);

        return ToResult(settlementCase, vat, journal, isDuplicate: false);
    }

    public async Task<SettlementResaleTriggerResult> ReversePrincipalResaleAsync(
        SettlementResaleReversalRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _caseStore.FindByKeyAsync(
            request.Book,
            request.ReversalExternalTransactionId,
            cancellationToken);
        if (existing != null)
        {
            return BuildDuplicateResult(existing);
        }

        var line = CostMarkupLine.Of(request.BuyPrice, request.SellPrice);
        var vat = _vatCalculator.Compute(line, request.VatRate, VatTreatment.Principal);
        var profile = _profileResolver.Resolve(request.Book);
        var originalJournal = PrincipalResaleJournalBuilder.Build(
            line,
            vat,
            request.Book,
            request.PostedAt,
            Guid.NewGuid(),
            request.Reference);

        BookIsolationGuard.EnsureWithinBook(profile, originalJournal);

        var reversalJournal = originalJournal.Reverse(
            Guid.NewGuid(),
            request.PostedAt,
            request.Reference ?? $"Reversal of {request.OriginalSettlementCaseId:D}");

        BookIsolationGuard.EnsureWithinBook(profile, reversalJournal);

        var reversalCase = SettlementCase.Start(
            Guid.NewGuid(),
            request.Book,
            request.PartnerId,
            request.ReversalExternalTransactionId,
            request.PostedAt);
        reversalCase.LinkReversal(request.OriginalSettlementCaseId);
        reversalCase.TransitionTo(SettlementCaseState.Allocated, request.PostedAt);
        await _caseStore.InsertAsync(reversalCase, cancellationToken);

        return ToResult(reversalCase, vat, reversalJournal, isDuplicate: false);
    }

    private static SettlementResaleTriggerResult BuildDuplicateResult(SettlementCase existing) =>
        new()
        {
            SettlementCaseId = existing.Id,
            IsDuplicate = true,
            ReversesSettlementCaseId = existing.ReversesSettlementCaseId
        };

    private static SettlementResaleTriggerResult ToResult(
        SettlementCase settlementCase,
        ResaleVatResult vat,
        Journal journal,
        bool isDuplicate) =>
        new()
        {
            SettlementCaseId = settlementCase.Id,
            IsDuplicate = isDuplicate,
            ReversesSettlementCaseId = settlementCase.ReversesSettlementCaseId,
            OutputVat = vat.OutputVat.Amount,
            InputVat = vat.InputVat.Amount,
            Margin = vat.Margin.Amount,
            NetVatToZatca = vat.NetVatToZatca.Amount,
            TotalDebits = journal.TotalDebits.Amount,
            TotalCredits = journal.TotalCredits.Amount,
            JournalLegs = journal.Lines.Select(l => new SettlementResaleJournalLegDto
            {
                Account = l.Account,
                Direction = l.Direction,
                Amount = l.Amount.Amount,
                Currency = l.Amount.Currency
            }).ToList()
        };
}
