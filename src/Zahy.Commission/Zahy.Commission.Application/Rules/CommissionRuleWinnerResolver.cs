using System.Linq;
using Volo.Abp;

namespace Zahy.Commission;

public class CommissionRuleWinnerResolver : ICommissionRuleWinnerResolver
{
    public IReadOnlyList<CommissionRuleSnapshot> ResolveWinningRules(IEnumerable<CommissionRuleSnapshot> candidateRules)
    {
        Check.NotNull(candidateRules, nameof(candidateRules));

        return candidateRules
            .GroupBy(rule => rule.FeeType)
            .Select(group => group
                .OrderByDescending(rule => CommissionScopePrecedence.Get(rule.ScopeKind))
                .ThenByDescending(rule => rule.Priority)
                .ThenBy(rule => rule.Id)
                .First())
            .ToList();
    }
}
