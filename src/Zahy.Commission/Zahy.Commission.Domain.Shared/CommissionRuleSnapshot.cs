namespace Zahy.Commission;

public sealed class CommissionRuleSnapshot
{
    public Guid Id { get; init; }

    public CommissionFeeType FeeType { get; init; }

    public CommissionBasisAmountKind BasisAmountKind { get; init; } = CommissionBasisAmountKind.Subtotal;

    public CommissionScopeKind ScopeKind { get; init; }

    public int Priority { get; init; }

    public CommissionBasisDefinition BasisDefinition { get; init; } = new();
}
