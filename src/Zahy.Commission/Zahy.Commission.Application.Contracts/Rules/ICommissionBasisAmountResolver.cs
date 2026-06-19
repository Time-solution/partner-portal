namespace Zahy.Commission;

public interface ICommissionBasisAmountResolver
{
    decimal Resolve(CommissionOrderAmounts orderAmounts, CommissionRuleSnapshot rule, string? productSku = null);
}

public interface ICommissionRuleWinnerResolver
{
    /// <summary>
    /// Returns at most one winning rule per <see cref="CommissionFeeType"/>.
    /// Tie-break: scope precedence (Product &gt; Category &gt; Partner &gt; PartnerType),
    /// then Priority, then Id.
    /// </summary>
    IReadOnlyList<CommissionRuleSnapshot> ResolveWinningRules(IEnumerable<CommissionRuleSnapshot> candidateRules);
}
