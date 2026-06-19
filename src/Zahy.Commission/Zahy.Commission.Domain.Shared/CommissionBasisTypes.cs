namespace Zahy.Commission;

public sealed class CommissionTier
{
    public decimal FromAmount { get; init; }

    public decimal? ToAmount { get; init; }

    public decimal? Rate { get; init; }

    public decimal? FlatFee { get; init; }
}

public sealed class TieredCommissionDefinition
{
    public TieredCommissionMode Mode { get; init; } = TieredCommissionMode.Bracket;

    public IReadOnlyList<CommissionTier> Tiers { get; init; } = [];
}

public sealed class CommissionBasisDefinition
{
    public decimal? FlatFee { get; init; }

    public decimal? PercentageRate { get; init; }

    public TieredCommissionDefinition? Tiered { get; init; }

    public decimal? MinCommission { get; init; }

    public decimal? MaxCommission { get; init; }

    public decimal? CapCommission { get; init; }
}

public sealed class CommissionCalculationResult
{
    public decimal BasisAmount { get; init; }

    public decimal RawComponentTotal { get; init; }

    public decimal ComputedCommission { get; init; }

    public string Currency { get; init; } = CommissionConsts.DefaultCurrency;
}
