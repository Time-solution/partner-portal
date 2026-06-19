namespace Zahy.Connectors;

public enum CanonicalOrderStatus
{
    Received = 1,
    PendingAcceptance = 2,
    Accepted = 3,
    Rejected = 4,
    InPreparation = 5,
    ReadyForHandoff = 6,
    InTransit = 7,
    Delivered = 8,
    Cancelled = 9,
    Returned = 10,
    Failed = 11
}
