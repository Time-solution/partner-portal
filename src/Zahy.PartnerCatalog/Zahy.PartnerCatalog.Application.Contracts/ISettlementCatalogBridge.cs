namespace Zahy.PartnerCatalog;

public enum SettlementCatalogBridgeOutcome
{
    Dispatched = 1,
    Skipped = 2,
    Duplicate = 3,
    Reversed = 4
}

public sealed class SettlementCatalogBridgeResult
{
    public SettlementCatalogBridgeOutcome Outcome { get; init; }

    public Guid? SettlementCaseId { get; init; }

    public Guid? ReversesSettlementCaseId { get; init; }

    public string? SkipReason { get; init; }

    public decimal OutputVat { get; init; }

    public decimal InputVat { get; init; }

    public decimal Margin { get; init; }

    public decimal NetVatToZatca { get; init; }

    public decimal TotalDebits { get; init; }

    public decimal TotalCredits { get; init; }
}

/// <summary>
/// Bridges <see cref="SettlementCostMarkupSnapshot"/> rows into the settlement engine (2b/2c).
/// </summary>
public interface ISettlementCatalogBridge
{
    Task<SettlementCatalogBridgeResult> DispatchAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken = default);

    Task<SettlementCatalogBridgeResult> ReverseAsync(
        SettlementCostMarkupSnapshot originalSnapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken = default);
}
