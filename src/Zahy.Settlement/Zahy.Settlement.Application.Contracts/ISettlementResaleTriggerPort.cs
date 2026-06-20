using System;
using System.Collections.Generic;

namespace Zahy.Settlement;

public sealed class SettlementResaleJournalLegDto
{
    public SettlementAccountType Account { get; init; }

    public EntryDirection Direction { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = SettlementConsts.DefaultCurrency;
}

public sealed class SettlementResaleTriggerRequest
{
    public SettlementBook Book { get; init; }

    public Guid PartnerId { get; init; }

    public string ExternalTransactionId { get; init; } = string.Empty;

    public Money BuyPrice { get; init; } = Money.Zero();

    public Money SellPrice { get; init; } = Money.Zero();

    public decimal VatRate { get; init; } = 0.15m;

    public DateTime PostedAt { get; init; }

    public string? Reference { get; init; }
}

public sealed class SettlementResaleReversalRequest
{
    public SettlementBook Book { get; init; }

    public Guid PartnerId { get; init; }

    public Guid OriginalSettlementCaseId { get; init; }

    public string ReversalExternalTransactionId { get; init; } = string.Empty;

    public Money BuyPrice { get; init; } = Money.Zero();

    public Money SellPrice { get; init; } = Money.Zero();

    public decimal VatRate { get; init; } = 0.15m;

    public DateTime PostedAt { get; init; }

    public string? Reference { get; init; }
}

public sealed class SettlementResaleTriggerResult
{
    public Guid SettlementCaseId { get; init; }

    public bool IsDuplicate { get; init; }

    public Guid? ReversesSettlementCaseId { get; init; }

    public decimal OutputVat { get; init; }

    public decimal InputVat { get; init; }

    public decimal Margin { get; init; }

    public decimal NetVatToZatca { get; init; }

    public decimal TotalDebits { get; init; }

    public decimal TotalCredits { get; init; }

    public IReadOnlyList<SettlementResaleJournalLegDto> JournalLegs { get; init; } =
        Array.Empty<SettlementResaleJournalLegDto>();
}

/// <summary>
/// Anti-corruption port: Partner Catalog (and future callers) trigger principal resale settlement.
/// Computation + case creation only — disbursement and live ZATCA stay flagged OFF.
/// </summary>
public interface ISettlementResaleTriggerPort
{
    Task<SettlementResaleTriggerResult> TriggerPrincipalResaleAsync(
        SettlementResaleTriggerRequest request,
        CancellationToken cancellationToken = default);

    Task<SettlementResaleTriggerResult> ReversePrincipalResaleAsync(
        SettlementResaleReversalRequest request,
        CancellationToken cancellationToken = default);
}
