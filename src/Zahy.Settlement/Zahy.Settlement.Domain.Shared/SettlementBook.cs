namespace Zahy.Settlement;

/// <summary>
/// Per-partner-type book. Each book has its own account tree, its own journals, and its own
/// settlement cases — the unit of isolation so the accountant can follow each partner type
/// independently (DESIGN.md §6/§6.1). Mapping of PartnerType → Book is configuration, decided
/// by the accountant before go-live (DESIGN.md §11.2).
/// </summary>
public enum SettlementBook
{
    /// <summary>Aggregator / Marketplace (Model 2): collected-total split flow.</summary>
    Marketplace = 1,

    /// <summary>Service / Integration (Model 3): cost + markup resale flow.</summary>
    Integration = 2
}
