using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Settlement;

/// <summary>Minimal persistence contract for settlement cases (implemented over ABP repositories in EFCore;
/// faked in unit tests). Keeps the engine logic testable without a database.</summary>
public interface ISettlementCaseStore
{
    Task<SettlementCase?> FindByKeyAsync(SettlementBook book, string externalTransactionId, CancellationToken cancellationToken = default);

    Task InsertAsync(SettlementCase settlementCase, CancellationToken cancellationToken = default);

    Task UpdateAsync(SettlementCase settlementCase, CancellationToken cancellationToken = default);
}

/// <summary>Append-only event-log store.</summary>
public interface ISettlementEventStore
{
    Task InsertAsync(SettlementWebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettlementWebhookEvent>> GetByCaseAsync(Guid settlementCaseId, CancellationToken cancellationToken = default);
}
