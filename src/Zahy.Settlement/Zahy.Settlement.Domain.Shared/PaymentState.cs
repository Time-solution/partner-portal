namespace Zahy.Settlement;

/// <summary>
/// Payment-status of a single AR position (a balance owed to Zahy), derived purely from cumulative
/// payments vs the AR total. This is a SEPARATE concept from the <see cref="SettlementCaseState"/>
/// lifecycle (Collected → … → Reconciled), which is left untouched. The progression here advances
/// past the point where the AR is booked (Allocated):
///   Allocated → PartiallyPaid → Paid.
///   • Allocated      = AR booked, nothing received yet (paidToDate == 0).
///   • PartiallyPaid  = some received, balance still open (0 &lt; paidToDate &lt; arTotal).
///   • Paid           = fully received (paidToDate == arTotal).
/// </summary>
public enum PaymentState
{
    Allocated = 1,
    PartiallyPaid = 2,
    Paid = 3
}
