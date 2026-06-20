namespace Zahy.Settlement;

/// <summary>The settlement lifecycle. Advances one legal step at a time (see SettlementStateMachine).</summary>
public enum SettlementCaseState
{
    Collected = 1,
    Allocated = 2,
    Invoiced = 3,
    Cleared = 4,
    Disbursed = 5,
    Reconciled = 6
}
