namespace Zahy.Finance;

public static class FinanceAccountStatusRules
{
    public static bool CanTransition(FinanceAccountStatus fromStatus, FinanceAccountStatus toStatus) =>
        (fromStatus, toStatus) switch
        {
            (FinanceAccountStatus.Pending, FinanceAccountStatus.Active) => true,
            (FinanceAccountStatus.Active, FinanceAccountStatus.Suspended) => true,
            (FinanceAccountStatus.Suspended, FinanceAccountStatus.Active) => true,
            (FinanceAccountStatus.Active, FinanceAccountStatus.Closed) => true,
            (FinanceAccountStatus.Suspended, FinanceAccountStatus.Closed) => true,
            _ => false
        };
}
