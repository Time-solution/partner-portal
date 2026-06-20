using System;

namespace Zahy.Settlement;

/// <summary>
/// Inputs to allocate a collected settlement. The component amounts are parsed from the provider's
/// payload by the ingestion adapter. PlatformCommissionInclusive is VAT-inclusive; the allocator
/// splits it into net + output VAT using the round-per-line back-out (DESIGN.md §9.1).
/// </summary>
public sealed record SettlementAllocationInput
{
    public SettlementBook Book { get; init; }

    public Guid PartnerId { get; init; }

    public string ExternalTransactionId { get; init; } = string.Empty;

    public Money CollectedTotal { get; init; } = Money.Zero();

    public Money MerchantPayout { get; init; } = Money.Zero();

    public Money PlatformCommissionInclusive { get; init; } = Money.Zero();

    public Money DeliveryCost { get; init; } = Money.Zero();

    public decimal VatRate { get; init; }
}

/// <summary>The posted balanced journal plus the breakdown shown by the explain endpoint.</summary>
public sealed record SettlementAllocationResult
{
    public required Journal Journal { get; init; }

    public Money MerchantPayout { get; init; } = Money.Zero();

    public Money PlatformCommissionNet { get; init; } = Money.Zero();

    public Money DeliveryCost { get; init; } = Money.Zero();

    public Money VatOutput { get; init; } = Money.Zero();

    public Money VatInput { get; init; } = Money.Zero();

    public Money NetVatToZatca { get; init; } = Money.Zero();
}
