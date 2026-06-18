namespace Zahy.PartnerPlatform.Partners;

/// <summary>Pure lifecycle rules: allowed transitions only.</summary>
public static class PartnerLifecyclePolicy
{
    public static bool TryGetTargetStatus(
        PartnerStatus current,
        PartnerLifecycleAction action,
        out PartnerStatus target)
    {
        target = current;

        return (current, action) switch
        {
            (PartnerStatus.Pending, PartnerLifecycleAction.Approve) => Set(PartnerStatus.Active, out target),
            (PartnerStatus.Pending, PartnerLifecycleAction.Reject) => Set(PartnerStatus.Closed, out target),
            (PartnerStatus.Active, PartnerLifecycleAction.Suspend) => Set(PartnerStatus.Suspended, out target),
            (PartnerStatus.Active, PartnerLifecycleAction.Close) => Set(PartnerStatus.Closed, out target),
            (PartnerStatus.Suspended, PartnerLifecycleAction.Reactivate) => Set(PartnerStatus.Active, out target),
            (PartnerStatus.Suspended, PartnerLifecycleAction.Close) => Set(PartnerStatus.Closed, out target),
            _ => false
        };
    }

    private static bool Set(PartnerStatus status, out PartnerStatus target)
    {
        target = status;
        return true;
    }
}
