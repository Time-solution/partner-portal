using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Settlement;

public sealed record SettlementEventDto
{
    public Guid Id { get; init; }
    public SettlementEventOutcome Outcome { get; init; }
    public WebhookSignatureStatus SignatureStatus { get; init; }
    public SettlementCaseState? ResultingState { get; init; }
    public DateTime ReceivedAt { get; init; }
}

/// <summary>The full breakdown for one settlement: state, allocation/journal, and the linked webhook events.</summary>
public sealed record SettlementExplainDto
{
    public Guid SettlementCaseId { get; init; }
    public SettlementBook Book { get; init; }
    public Guid PartnerId { get; init; }
    public string ExternalTransactionId { get; init; } = string.Empty;
    public SettlementCaseState State { get; init; }
    public SettlementAllocationSnapshot? Allocation { get; init; }
    public List<SettlementEventDto> Events { get; init; } = new();
}

public interface ISettlementExplainService
{
    Task<SettlementExplainDto?> ExplainAsync(SettlementBook book, string externalTransactionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only join: settlement case → its allocation/journal snapshot → the webhook events that
/// triggered it. RBAC + per-book scoping are applied by the HTTP layer when hosted (DESIGN.md §13).
/// </summary>
public sealed class SettlementExplainService : ISettlementExplainService
{
    private readonly ISettlementCaseStore _caseStore;
    private readonly ISettlementEventStore _eventStore;

    public SettlementExplainService(ISettlementCaseStore caseStore, ISettlementEventStore eventStore)
    {
        _caseStore = caseStore;
        _eventStore = eventStore;
    }

    public async Task<SettlementExplainDto?> ExplainAsync(SettlementBook book, string externalTransactionId, CancellationToken cancellationToken = default)
    {
        var settlementCase = await _caseStore.FindByKeyAsync(book, externalTransactionId, cancellationToken);
        if (settlementCase == null)
        {
            return null;
        }

        var events = await _eventStore.GetByCaseAsync(settlementCase.Id, cancellationToken);
        var allocationJson = events
            .Where(e => e.Outcome == SettlementEventOutcome.Allocated && e.AllocationSnapshotJson != null)
            .Select(e => e.AllocationSnapshotJson)
            .FirstOrDefault();

        return new SettlementExplainDto
        {
            SettlementCaseId = settlementCase.Id,
            Book = settlementCase.Book,
            PartnerId = settlementCase.PartnerId,
            ExternalTransactionId = settlementCase.ExternalTransactionId,
            State = settlementCase.State,
            Allocation = SettlementAllocationSnapshot.FromJson(allocationJson),
            Events = events.Select(e => new SettlementEventDto
            {
                Id = e.Id,
                Outcome = e.Outcome,
                SignatureStatus = e.SignatureStatus,
                ResultingState = e.ResultingState,
                ReceivedAt = e.ReceivedAt
            }).ToList()
        };
    }
}
