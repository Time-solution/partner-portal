namespace Zahy.Connectors;

public interface IAcceptancePolicyEvaluator
{
    bool IsExpired(CanonicalOrder order, DateTime utcNow);

    bool CanAccept(CanonicalOrder order, DateTime utcNow);
}
