namespace Zahy.Connectors;

public class AcceptancePolicyEvaluator : IAcceptancePolicyEvaluator
{
    public bool IsExpired(CanonicalOrder order, DateTime utcNow) =>
        order.AcceptDeadlineUtc.HasValue && utcNow > order.AcceptDeadlineUtc.Value;

    public bool CanAccept(CanonicalOrder order, DateTime utcNow) =>
        order.Status == CanonicalOrderStatus.PendingAcceptance && !IsExpired(order, utcNow);
}
