namespace Zahy.Settlement;

/// <summary>
/// Partner-type portfolio an account is scoped to. Codes are shared across the chart: the core
/// accounts seeded in Phase A are <see cref="Shared"/> (used by both the Aggregator and Service
/// portfolios). Portfolio-specific accounts (future) scope to <see cref="Aggregator"/> or
/// <see cref="Service"/>.
/// </summary>
public enum AccountPortfolio
{
    /// <summary>Common backbone account used across all partner-type portfolios.</summary>
    Shared = 1,

    /// <summary>Aggregator / Marketplace portfolio (Model 2).</summary>
    Aggregator = 2,

    /// <summary>Service / Integration portfolio (Model 3).</summary>
    Service = 3
}
