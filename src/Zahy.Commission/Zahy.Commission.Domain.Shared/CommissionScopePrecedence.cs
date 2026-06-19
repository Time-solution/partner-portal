namespace Zahy.Commission;

public static class CommissionScopePrecedence
{
    /// <summary>Product &gt; Category &gt; Partner &gt; PartnerType.</summary>
    public static int Get(CommissionScopeKind scopeKind) =>
        scopeKind switch
        {
            CommissionScopeKind.Product => 4,
            CommissionScopeKind.Category => 3,
            CommissionScopeKind.Partner => 2,
            CommissionScopeKind.PartnerType => 1,
            _ => 0
        };
}
